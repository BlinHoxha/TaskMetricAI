using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;
using TaskMetricAI.Infrastructure.Persistence;

namespace TaskMetricAI.Backend.Tests;

[Trait("Category", "PostgreSQL")]
public sealed class RuntimeSecurityTests
{
    [Theory]
    [InlineData("CREATE TABLE public.runtime_escalation (id integer)")]
    [InlineData("TRUNCATE TABLE \"AuditEvent\"")]
    [InlineData("ALTER TABLE \"ProfileVersion\" DISABLE ROW LEVEL SECURITY")]
    [InlineData("SET ROLE taskmetric_setup")]
    [InlineData("ALTER ROLE taskmetric_runtime_test SUPERUSER")]
    [InlineData("SELECT * FROM \"TenantInstallation\"")]
    [InlineData("DROP TRIGGER profile_lifecycle ON \"ProfileVersion\"")]
    [InlineData("UPDATE \"ProfileVersion\" SET \"SnapshotJson\"='{}' WHERE false")]
    [InlineData("DELETE FROM \"MetricVersion\" WHERE false")]
    [InlineData("UPDATE \"AuditEvent\" SET \"Reason\"='rewritten' WHERE false")]
    [InlineData("CREATE POLICY runtime_bypass ON \"ProfileVersion\" USING (true)")]
    public async Task Runtime_cannot_escalate_privileges_or_change_schema(string sql)
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await using var connection = new NpgsqlConnection(fixture.Runtime); await connection.OpenAsync();
        await RuntimeDatabase.ValidateRoleAsync(connection);
        await using var command = new NpgsqlCommand(sql, connection);
        var denied = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
    }

    [Fact]
    public async Task Transaction_local_tenant_is_cleared_on_commit_rollback_exception_and_cancellation()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var settings = new NpgsqlConnectionStringBuilder(fixture.Runtime) { MaxPoolSize = 1 };
        int firstPid;
        await using (var session = await fixture.Session(fixture.A, connection: settings.ConnectionString))
        {
            firstPid = ((NpgsqlConnection)session.Db.Database.GetDbConnection()).ProcessID;
            Assert.All(await session.Db.Set<ScopeGrant>().IgnoreQueryFilters().ToArrayAsync(), x => Assert.Equal(fixture.A, x.TenantId));
            await session.CommitAsync();
        }
        await using (var next = await fixture.Session(fixture.B, connection: settings.ConnectionString))
        {
            Assert.Equal(firstPid, ((NpgsqlConnection)next.Db.Database.GetDbConnection()).ProcessID);
            Assert.All(await next.Db.Set<ScopeGrant>().IgnoreQueryFilters().ToArrayAsync(), x => Assert.Equal(fixture.B, x.TenantId));
            // Leaving without commit rolls back; no tenant setting may survive.
        }
        await using (var interrupted = await fixture.Session(fixture.A, connection: settings.ConnectionString))
        {
            interrupted.Db.Add(new AuditEvent { TenantId = fixture.A, ActorId = "admin", Operation = "cancelled-write",
                SubjectId = "test", Reason = "must roll back", OccurredAt = DateTimeOffset.UtcNow });
            await interrupted.Db.SaveChangesAsync();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => interrupted.Db.Database.ExecuteSqlRawAsync("SELECT pg_sleep(5)", cancellation.Token));
        }
        await using (var probe = new NpgsqlConnection(settings.ConnectionString))
        {
            await probe.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT NULLIF(current_setting('taskmetric.tenant', true), '') IS NULL", probe);
            Assert.Equal(true, await command.ExecuteScalarAsync());
        }
        await using var verify = await fixture.Session(fixture.A, connection: settings.ConnectionString);
        Assert.False(await verify.Db.Set<AuditEvent>().AnyAsync(x => x.Operation == "cancelled-write"));
    }

    [Fact]
    public async Task Already_open_connection_cannot_be_used_with_forged_or_missing_tenant_context()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await using var owner = await fixture.Session(fixture.A);
        var connection = owner.Db.Database.GetDbConnection();
        await using var forged = new ConfigurationDbContext(new DbContextOptionsBuilder<ConfigurationDbContext>().UseNpgsql(connection).Options,
            new(fixture.B, "org", "admin", "cloud", "install", "app", "env"));
        await forged.Database.UseTransactionAsync(owner.Db.Database.CurrentTransaction!.GetDbTransaction());
        await Assert.ThrowsAsync<OperationException>(() => forged.Set<ScopeGrant>().IgnoreQueryFilters().ToArrayAsync());
        await using var missing = new ConfigurationDbContext(new DbContextOptionsBuilder<ConfigurationDbContext>().UseNpgsql(fixture.Runtime).Options, owner.Identity);
        await Assert.ThrowsAsync<OperationException>(() => missing.Set<ScopeGrant>().ToArrayAsync());
        var unsafeSettings = new NpgsqlConnectionStringBuilder(fixture.Runtime) { NoResetOnClose = true };
        Assert.Throws<OperationException>(() => RuntimeDatabase.ValidateSettings(unsafeSettings.ConnectionString));
    }

    [Fact]
    public async Task Raw_foreign_mutations_are_filtered_and_foreign_insert_and_content_changes_are_rejected()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        foreach (var state in new[] { (ProfileStatus.Validated, 0L), (ProfileStatus.Active, 1L) })
        {
            await using var b = await fixture.Session(fixture.B);
            await fixture.Service(b).TransitionAsync(fixture.ProfileB, 1, state.Item1, state.Item2, "prepare foreign resource"); await b.CommitAsync();
        }
        await using (var b = await fixture.Session(fixture.B))
        { await fixture.Service(b).AssignAsync(new(ScopeType.Organization, "org"), fixture.ProfileB, 1, "prepare foreign assignment"); await b.CommitAsync(); }
        await using var session = await fixture.Session(fixture.A);
        Assert.Equal(0, await session.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"ProfileVersion\" SET \"Status\"=1,\"Revision\"=1 WHERE \"TenantId\"={fixture.B}"));
        Assert.Equal(0, await session.Db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"ProfileAssignment\" WHERE \"TenantId\"={fixture.B}"));
        var denied = await Assert.ThrowsAsync<PostgresException>(() => session.Db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"AuditEvent\" (\"TenantId\",\"Id\",\"ActorId\",\"Operation\",\"SubjectId\",\"Reason\",\"OccurredAt\",\"TargetType\") VALUES ({fixture.B},{Guid.NewGuid().ToString()},{"admin"},{"forged"},{"target"},{"test"},{DateTimeOffset.UtcNow},{"test"})"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        await using var verify = await fixture.Session(fixture.B);
        Assert.Single(await verify.Db.Set<ProfileAssignment>().ToArrayAsync());
        Assert.Equal(ProfileStatus.Active, (await fixture.Service(verify).ReadProfileAsync(fixture.ProfileB, 1)).Status);
    }

    [Fact]
    public async Task An_exception_rolls_back_data_and_cannot_leave_a_tenant_on_a_reused_connection()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var session = await fixture.Session(fixture.A);
            session.Db.Add(new AuditEvent { TenantId = fixture.A, ActorId = "admin", Operation = "exception-write", SubjectId = "test",
                Reason = "must roll back", OccurredAt = DateTimeOffset.UtcNow });
            await session.Db.SaveChangesAsync();
            throw new InvalidOperationException("synthetic application exception");
        });
        await using var verify = await fixture.Session(fixture.A);
        Assert.False(await verify.Db.Set<AuditEvent>().AnyAsync(x => x.Operation == "exception-write"));
    }
}
