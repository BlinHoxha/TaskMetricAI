using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskMetricAI.Domain;

namespace TaskMetricAI.Infrastructure.Persistence;

/// <summary>Explicit operator action for the isolated local databases. Never run on request/startup.</summary>
public static class PostgreSqlSchema
{
    public const int Version = 1;
    public static async Task InitializeLocalAsync(string connectionString, CancellationToken ct = default)
    {
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        Require.That(settings.Database is "taskmetricai_dev" or "taskmetricai_test", 422, "isolated_database_required");
        Require.That(settings.Host is "localhost" or "127.0.0.1" or "::1", 422, "local_database_required");
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(ct);
        await using (var role = new NpgsqlCommand("SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user", connection))
            Require.That(await role.ExecuteScalarAsync(ct) is false, 403, "non_privileged_database_role_required");
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await Execute("SELECT pg_advisory_xact_lock(761742013)", connection, ct);
        await using var probe = new NpgsqlCommand("SELECT to_regclass('public.\"TaskMetricSchema\"') IS NOT NULL", connection);
        if (await probe.ExecuteScalarAsync(ct) is true)
        {
            await using var version = new NpgsqlCommand("SELECT \"Version\" FROM \"TaskMetricSchema\"", connection);
            Require.That(await version.ExecuteScalarAsync(ct) is int number && number is Version or 2 or 3, 409, "unsupported_database_schema");
            await transaction.CommitAsync(ct); return;
        }
        // Never adopt a pre-existing database with unrelated application tables.
        await using var tables = new NpgsqlCommand("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'", connection);
        Require.That(Convert.ToInt64(await tables.ExecuteScalarAsync(ct)) == 0, 409, "empty_database_required");
        var tenant = new TenantIdentity("schema", "schema", "operator", "cloud", "install", "app", "env");
        await using var db = new ConfigurationDbContext(new DbContextOptionsBuilder<ConfigurationDbContext>().UseNpgsql(connectionString).Options, tenant);
        await using var directory = new InstallationDbContext(new DbContextOptionsBuilder<InstallationDbContext>().UseNpgsql(connectionString).Options);
        await Execute(db.Database.GenerateCreateScript(), connection, ct);
        await Execute(directory.Database.GenerateCreateScript(), connection, ct);
        foreach (var entity in db.Model.GetEntityTypes())
        {
            var table = entity.GetTableName()!; // Fixed model metadata, never caller-supplied identifiers.
            await Execute($"""
                ALTER TABLE "{table}" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE "{table}" FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON "{table}"
                    USING ("TenantId" = current_setting('taskmetric.tenant', true))
                    WITH CHECK ("TenantId" = current_setting('taskmetric.tenant', true));
                """, connection, ct);
        }
        await Execute("""
            CREATE FUNCTION taskmetric_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'immutable configuration record' USING ERRCODE = '23514'; END $$;
            CREATE FUNCTION taskmetric_profile_lifecycle() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'immutable profile' USING ERRCODE = '23514'; END IF;
              IF (to_jsonb(NEW) - 'Status' - 'Revision') IS DISTINCT FROM (to_jsonb(OLD) - 'Status' - 'Revision')
                 OR NEW."Revision" <> OLD."Revision" + 1
                 OR NOT ((OLD."Status" = 0 AND NEW."Status" = 1) OR (OLD."Status" = 1 AND NEW."Status" = 2)
                     OR (OLD."Status" = 2 AND NEW."Status" = 3))
              THEN RAISE EXCEPTION 'invalid profile mutation' USING ERRCODE = '23514'; END IF;
              RETURN NEW;
            END $$;
            CREATE TRIGGER profile_lifecycle BEFORE UPDATE OR DELETE ON "ProfileVersion"
              FOR EACH ROW EXECUTE FUNCTION taskmetric_profile_lifecycle();
            """, connection, ct);
        foreach (var table in new[] { "MetricVersion", "CatalogVersion", "CatalogEntry", "AuditEvent", "PinnedRepositorySnapshot" })
            await Execute($"CREATE TRIGGER immutable_record BEFORE UPDATE OR DELETE ON \"{table}\" FOR EACH ROW EXECUTE FUNCTION taskmetric_immutable();", connection, ct);
        await Execute("CREATE TABLE \"TaskMetricSchema\" (\"Version\" integer PRIMARY KEY CHECK (\"Version\" = 1)); INSERT INTO \"TaskMetricSchema\" VALUES (1);", connection, ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task Execute(string sql, NpgsqlConnection connection, CancellationToken ct)
    { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(ct); }
}
