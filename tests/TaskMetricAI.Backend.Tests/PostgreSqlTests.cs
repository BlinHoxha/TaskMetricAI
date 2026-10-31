using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;
using TaskMetricAI.Infrastructure.Persistence;
using TaskMetricAI.Infrastructure.Authentication;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

namespace TaskMetricAI.Backend.Tests;

/// <summary>Runs against an already provisioned isolated PostgreSQL 17 test database; never drops a database.</summary>
[Trait("Category", "PostgreSQL")]
public sealed class PostgreSqlTests
{
    [PostgresFact]
    public async Task Signed_identity_resolves_only_its_registered_tenant_and_operator_bootstrap_is_atomic()
    {
        var connection = await Setup(); var tenant = Guid.NewGuid().ToString("N");
        const string cloud = "11111111-1111-1111-1111-111111111111";
        var binding = new TenantInstallation { TenantId = tenant, OrganizationId = "org", AppId = "app-id",
            EnvironmentId = "environment", InstallationId = "installation", CloudId = cloud };
        // Each test execution uses its own installation tuple without changing the signed payload shape.
        binding.AppId = "app-" + tenant;
        await LocalDatabaseOperator.RegisterInstallationAsync(connection, binding, "actor", "synthetic test registration");
        await using var directoryDb = new InstallationDbContext(new DbContextOptionsBuilder<InstallationDbContext>().UseNpgsql(connection).Options);
        using var rsa = RSA.Create(2048); var key = new RsaSecurityKey(rsa) { KeyId = "local-test" };
        var verifier = new ForgeIdentityVerifier(new(binding.AppId, ["DEVELOPMENT"]), new SigningKeys(key));
        var authenticate = new AuthenticateTenant(verifier, new InstallationDirectory(directoryDb));
        var identity = await authenticate.ExecuteAsync(AuthenticationTests.Token(key, audience: binding.AppId, appId: binding.AppId));
        Assert.Equal(tenant, identity.TenantId);
        await using var db = Context(connection, tenant);
        await new AccessControl(new TenantRepository(db), identity).RequireAdministratorAsync();
        var duplicateAdminBinding = new TenantInstallation { TenantId = tenant, OrganizationId = "org", AppId = binding.AppId,
            EnvironmentId = "other-env", InstallationId = "installation", CloudId = cloud };
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => LocalDatabaseOperator.RegisterInstallationAsync(connection,
            duplicateAdminBinding, "actor", "duplicate administrator must roll back binding"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
        Assert.False(await directoryDb.Set<TenantInstallation>().AnyAsync(x => x.AppId == binding.AppId && x.EnvironmentId == "other-env"));
        var tracked = await directoryDb.Set<TenantInstallation>().SingleAsync(x => x.AppId == binding.AppId);
        tracked.Enabled = false; await directoryDb.SaveChangesAsync();
        await Assert.ThrowsAsync<OperationException>(() => authenticate.ExecuteAsync(AuthenticationTests.Token(key, audience: binding.AppId, appId: binding.AppId)));
    }

    [PostgresFact]
    public async Task Concurrent_metric_versions_have_one_winner_and_no_orphan_audit()
    {
        var connection = await Setup(); var tenant = Guid.NewGuid().ToString("N");
        await using var seed = Context(connection, tenant); seed.Add(PersistenceTests.Grant(tenant)); await seed.SaveChangesAsync();
        async Task<bool> Attempt()
        {
            await using var db = Context(connection, tenant);
            var service = new ConfigurationService(new TenantRepository(db), AuthorizationTests.Identity(tenant), TimeProvider.System);
            try { await service.AddMetricVersionAsync("race", new("M", "D", new("1", "2", "3", "4", "5"), "race"), 0); return true; }
            catch (OperationException ex) when (ex.Status == 409) { return false; }
        }
        var results = await Task.WhenAll(Attempt(), Attempt());
        Assert.Single(results, x => x); Assert.Single(results, x => !x);
        Assert.Equal(1, await seed.Set<MetricVersion>().CountAsync());
        Assert.Equal(1, await seed.Set<AuditEvent>().CountAsync());
    }

    [PostgresFact]
    public async Task Database_RLS_blocks_filter_bypass_cross_tenant_queries_and_raw_writes()
    {
        var connection = await Setup(); var tenant = Guid.NewGuid().ToString("N");
        await using var a = Context(connection, tenant + "a");
        await using var b = Context(connection, tenant + "b");
        a.Add(PersistenceTests.Grant(tenant + "a", "shared")); await a.SaveChangesAsync();
        b.Add(PersistenceTests.Grant(tenant + "b", "shared")); await b.SaveChangesAsync();
        Assert.All(await a.Set<ScopeGrant>().IgnoreQueryFilters().ToArrayAsync(), row => Assert.Equal(tenant + "a", row.TenantId));
        // Force the SQL path without EF save guards. RLS must still reject a foreign tenant.
        var denied = await Assert.ThrowsAsync<PostgresException>(() => a.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"ScopeGrant\" (\"TenantId\",\"Id\",\"ActorId\",\"ScopeType\",\"ScopeId\",\"Role\") VALUES ({tenant + "b"},{"foreign"},{"actor"},{0},{"org"},{3})"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        await using var anonymous = new NpgsqlConnection(connection); await anonymous.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM \"ScopeGrant\"", anonymous);
        Assert.Equal(0L, await command.ExecuteScalarAsync());
    }

    [PostgresFact]
    public async Task PostgreSQL_composite_relationships_and_unique_versions_reject_invalid_data()
    {
        var connection = await Setup(); var tenant = Guid.NewGuid().ToString("N");
        await using var b = Context(connection, tenant + "b");
        var foreign = PersistenceTests.ProfileRow(tenant + "b"); b.Add(foreign); await b.SaveChangesAsync();
        await using var a = Context(connection, tenant + "a");
        a.Add(new ProfileAssignment { TenantId = tenant + "a", ProfileVersionId = foreign.Id,
            ScopeType = ScopeType.Organization, ScopeId = "org" });
        var relationship = await Assert.ThrowsAsync<DbUpdateException>(() => a.SaveChangesAsync()); a.ChangeTracker.Clear();
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(relationship.InnerException).SqlState);
        a.Add(PersistenceTests.ProfileRow(tenant + "a")); await a.SaveChangesAsync(); a.ChangeTracker.Clear();
        a.Add(PersistenceTests.ProfileRow(tenant + "a"));
        var version = await Assert.ThrowsAsync<DbUpdateException>(() => a.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(version.InnerException).SqlState);
    }

    [PostgresFact]
    public async Task PostgreSQL_history_triggers_reject_raw_updates_and_deletes()
    {
        var connection = await Setup(); var tenant = Guid.NewGuid().ToString("N");
        await using var db = Context(connection, tenant);
        var profile = PersistenceTests.ProfileRow(tenant); db.Add(profile);
        var audit = new AuditEvent { TenantId = tenant, ActorId = "actor", Operation = "test", Reason = "original", SubjectId = profile.Id };
        db.Add(audit); await db.SaveChangesAsync();
        var immutableProfile = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"ProfileVersion\" SET \"SnapshotJson\" = {"{}"} WHERE \"Id\" = {profile.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, immutableProfile.SqlState);
        var immutableAudit = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM \"AuditEvent\" WHERE \"Id\" = {audit.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, immutableAudit.SqlState);
        Assert.Equal(profile.ContentHash, Documents.Restore(await db.Set<ProfileVersion>().SingleAsync()).ContentHash);
    }

    [PostgresFact]
    public async Task PostgreSQL_versions_assignments_audit_and_concurrency_work_together()
    {
        var connection = await Setup(); var tenant = Guid.NewGuid().ToString("N");
        await using var db = Context(connection, tenant);
        db.Add(PersistenceTests.Grant(tenant)); await db.SaveChangesAsync();
        var identity = AuthorizationTests.Identity(tenant);
        var service = new ConfigurationService(new TenantRepository(db), identity, TimeProvider.System);
        var metric = await service.AddMetricVersionAsync("metric", new("M", "Custom", new("1", "2", "3", "4", "5"), "create"), 0);
        await service.AddCatalogVersionAsync("catalog", [metric.Reference], 0, "select");
        var first = await service.AddProfileVersionAsync("profile", PersistenceTests.Input(), 0);
        await service.TransitionAsync("profile", 1, ProfileStatus.Validated, 0, "validate");
        await service.TransitionAsync("profile", 1, ProfileStatus.Active, 1, "approve");
        await service.AssignAsync(first.Scope, "profile", 1, "assign");
        await using var staleContext = Context(connection, tenant);
        var stale = await staleContext.Set<ProfileVersion>().SingleAsync();
        await service.TransitionAsync("profile", 1, ProfileStatus.Archived, 2, "retire");
        stale.Status = ProfileStatus.Archived;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleContext.SaveChangesAsync());
        Assert.Empty(await db.Set<ProfileAssignment>().ToArrayAsync());
        Assert.Equal(first.ContentHash, (await service.ReadProfileAsync("profile", 1)).ContentHash);
        Assert.Equal(7, await db.Set<AuditEvent>().CountAsync());
    }

    private static ConfigurationDbContext Context(string connection, string tenant) =>
        new(new DbContextOptionsBuilder<ConfigurationDbContext>().UseNpgsql(connection).Options, AuthorizationTests.Identity(tenant), operatorOnly: true);

    private static async Task<string> Setup()
    {
        var path = Environment.GetEnvironmentVariable("TASKMETRICAI_TEST_CONNECTION_FILE")
            ?? throw new InvalidOperationException("TASKMETRICAI_TEST_CONNECTION_FILE is required; PostgreSQL security tests cannot be skipped.");
        var connection = (await File.ReadAllTextAsync(path)).Trim();
        var settings = new NpgsqlConnectionStringBuilder(connection);
        Require.That(settings.Database == "taskmetricai_test", 422, "test_database_required");
        await using var probe = new NpgsqlConnection(connection); await probe.OpenAsync();
        Require.That(probe.PostgreSqlVersion.Major == 17, 422, "postgresql_17_required");
        await PostgreSqlSchema.InitializeLocalAsync(connection);
        return connection;
    }

    private sealed class SigningKeys(SecurityKey key) : IForgeSigningKeys
    {
        public Task<IReadOnlyCollection<SecurityKey>> GetAsync(bool refresh, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<SecurityKey>>([key]);
    }
}

public sealed class PostgresFactAttribute : FactAttribute
{
}
