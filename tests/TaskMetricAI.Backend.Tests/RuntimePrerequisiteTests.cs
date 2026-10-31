using Npgsql;
using Microsoft.EntityFrameworkCore.Storage;
using TaskMetricAI.Domain;
using TaskMetricAI.Infrastructure.Persistence;

namespace TaskMetricAI.Backend.Tests;

[CollectionDefinition("Database prerequisites", DisableParallelization = true)]
public sealed class DatabasePrerequisiteCollection { }

[Collection("Database prerequisites")]
[Trait("Category", "PostgreSQL")]
public sealed class RuntimePrerequisiteTests
{
    [Theory]
    [InlineData("SET LOCAL default_transaction_isolation='repeatable read'")]
    [InlineData("SET LOCAL default_transaction_isolation='serializable'")]
    [InlineData("UPDATE public.\"TaskMetricSchema\" SET \"Version\"=2")]
    [InlineData("UPDATE public.\"TaskMetricSchema\" SET \"Version\"=4")]
    [InlineData("ALTER TABLE public.\"TaskMetricSchema\" RENAME TO \"TaskMetricSchema_unavailable\"")]
    [InlineData("REVOKE SELECT ON public.\"ProjectRegistration\" FROM taskmetric_runtime_test")]
    [InlineData("REVOKE SELECT ON public.\"TaskMetricSchema\" FROM taskmetric_runtime_test")]
    [InlineData("REVOKE DELETE ON public.\"ScopeGrant\" FROM taskmetric_runtime_test")]
    [InlineData("REVOKE UPDATE (\"Revision\") ON public.\"ProfileVersion\" FROM taskmetric_runtime_test")]
    [InlineData("REVOKE EXECUTE ON FUNCTION public.taskmetric_lookup_installation(text,text,text,text) FROM taskmetric_runtime_test")]
    [InlineData("GRANT INSERT ON public.\"ProjectRegistration\" TO taskmetric_runtime_test")]
    [InlineData("GRANT UPDATE ON public.\"TaskMetricSchema\" TO taskmetric_runtime_test")]
    [InlineData("ALTER TABLE public.\"ProfileVersion\" NO FORCE ROW LEVEL SECURITY")]
    public async Task Incompatible_prerequisites_fail_closed_and_changes_are_rolled_back(string mutation)
    {
        var setupFile = Environment.GetEnvironmentVariable("TASKMETRICAI_TEST_CONNECTION_FILE")
            ?? throw new InvalidOperationException("PostgreSQL setup reference is required.");
        var setup = (await File.ReadAllTextAsync(setupFile)).Trim();
        Assert.Equal("taskmetricai_test", new NpgsqlConnectionStringBuilder(setup).Database);
        await using var connection = new NpgsqlConnection(setup); await connection.OpenAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await using var command = new NpgsqlCommand(mutation, connection, transaction);
            await command.ExecuteNonQueryAsync();
            var rejected = await Assert.ThrowsAsync<OperationException>(() => RuntimeDatabase.ValidatePrerequisitesAsync(connection, "taskmetric_runtime_test"));
            Assert.Equal(503, rejected.Status);
            await transaction.RollbackAsync();
        }
        // No committed role, schema, grant or isolation change survives the negative test.
        var runtimeFile = Environment.GetEnvironmentVariable("TASKMETRICAI_RUNTIME_TEST_CONNECTION_FILE")
            ?? throw new InvalidOperationException("PostgreSQL runtime reference is required.");
        await using var runtime = new NpgsqlConnection((await File.ReadAllTextAsync(runtimeFile)).Trim());
        await runtime.OpenAsync(); await RuntimeDatabase.ValidateRoleAsync(runtime);
    }

    [Fact]
    public async Task Runtime_sessions_explicitly_use_read_committed()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await using var session = await fixture.Session(fixture.A);
        Assert.Equal(System.Data.IsolationLevel.ReadCommitted, session.Db.Database.CurrentTransaction!.GetDbTransaction().IsolationLevel);
    }

    [Fact]
    public async Task Runtime_connection_with_incompatible_isolation_fails_on_the_public_runtime_path()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        var settings = new NpgsqlConnectionStringBuilder(fixture.Runtime) { Options = "-c default_transaction_isolation=serializable" };
        var rejected = await Assert.ThrowsAsync<OperationException>(() => fixture.Session(fixture.A, connection: settings.ConnectionString));
        Assert.Equal(503, rejected.Status); Assert.Equal("runtime_prerequisites_missing", rejected.Code);
    }
}
