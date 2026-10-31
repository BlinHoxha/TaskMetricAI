using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;

namespace TaskMetricAI.Infrastructure.Persistence;

public static class RuntimeDatabase
{
    public static void ValidateSettings(string connectionString)
    {
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        Require.That(!settings.NoResetOnClose && !settings.Multiplexing && !settings.IncludeErrorDetail,
            503, "unsafe_runtime_connection_settings");
        Require.That((settings.Database, settings.Username) is ("taskmetricai_dev", "taskmetric_runtime_dev") or
            ("taskmetricai_test", "taskmetric_runtime_test"), 503, "runtime_database_identity_required");
    }

    public static async Task ValidateRoleAsync(NpgsqlConnection connection, CancellationToken ct = default)
    {
        await using var identity = new NpgsqlCommand("SELECT current_user", connection);
        var runtime = (string)(await identity.ExecuteScalarAsync(ct))!;
        await ValidatePrerequisitesAsync(connection, runtime, ct);
    }

    // The public runtime path always supplies current_user. Tests can inspect transactional ACL
    // changes as the owner without granting setup permission to SET ROLE into runtime identities.
    internal static async Task ValidatePrerequisitesAsync(NpgsqlConnection connection, string runtime, CancellationToken ct = default)
    {
        NpgsqlCommand Query(string sql)
        {
            var query = new NpgsqlCommand(sql.Replace("current_user", "@runtime", StringComparison.Ordinal), connection);
            if (sql.Contains("current_user", StringComparison.Ordinal)) query.Parameters.AddWithValue("runtime", runtime);
            return query;
        }
        await using var prerequisites = Query("""
            SELECT current_setting('default_transaction_isolation') = 'read committed'
              AND COALESCE(has_table_privilege(current_user,to_regclass('public."TaskMetricSchema"'),'SELECT'),false)
            """);
        Require.That(await prerequisites.ExecuteScalarAsync(ct) is true, 503, "runtime_prerequisites_missing");
        await using var schema = Query("SELECT count(*)=1 AND min(\"Version\")=3 FROM public.\"TaskMetricSchema\"");
        Require.That(await schema.ExecuteScalarAsync(ct) is true, 503, "runtime_schema_version_required");
        await using var command = Query("""
            SELECT NOT (r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolbypassrls OR r.rolreplication)
              AND NOT EXISTS (SELECT 1 FROM pg_class c WHERE c.relnamespace = 'public'::regnamespace AND c.relowner = r.oid)
              AND NOT EXISTS (SELECT 1 FROM pg_proc p WHERE p.pronamespace = 'public'::regnamespace AND p.proowner = r.oid)
              AND NOT has_schema_privilege(current_user, 'public', 'CREATE')
              AND NOT has_database_privilege(current_user, current_database(), 'CREATE')
              AND NOT EXISTS (SELECT 1 FROM pg_auth_members m WHERE m.member = r.oid)
              AND NOT EXISTS (SELECT 1 FROM pg_class c WHERE c.relnamespace='public'::regnamespace AND c.relkind='r'
                AND has_table_privilege(current_user,c.oid,'TRUNCATE'))
              AND NOT EXISTS (SELECT 1 FROM pg_class c WHERE c.relnamespace='public'::regnamespace AND c.relkind='r'
                AND c.relname <> 'TaskMetricSchema'
                AND has_table_privilege(current_user,c.oid,'SELECT') AND NOT (c.relrowsecurity AND c.relforcerowsecurity))
              AND to_regprocedure('public.taskmetric_lookup_installation(text,text,text,text)') IS NOT NULL
            FROM pg_roles r WHERE r.rolname = current_user
            """);
        Require.That(await command.ExecuteScalarAsync(ct) is true, 503, "unsafe_runtime_database_role");
        await using var grants = Query("""
            SELECT COALESCE(bool_and(COALESCE(has_table_privilege(current_user,
                to_regclass(format('public.%I',t)),p),false)),false)
            FROM (VALUES
              ('MetricVersion','SELECT'),('MetricVersion','INSERT'),('CatalogVersion','SELECT'),('CatalogVersion','INSERT'),
              ('CatalogEntry','SELECT'),('CatalogEntry','INSERT'),('AuditEvent','SELECT'),('AuditEvent','INSERT'),
              ('ProfileVersion','SELECT'),('ProfileVersion','INSERT'),('ScopeGrant','SELECT'),('ScopeGrant','INSERT'),('ScopeGrant','DELETE'),
              ('ProfileAssignment','SELECT'),('ProfileAssignment','INSERT'),('ProfileAssignment','DELETE'),
              ('ProjectRegistration','SELECT'),('TeamRegistration','SELECT'),('ProjectTeam','SELECT')) AS required(t,p)
            """);
        Require.That(await grants.ExecuteScalarAsync(ct) is true, 503, "runtime_security_grants_required");
        await using var columns = Query("""
            SELECT has_column_privilege(current_user,'public."ProfileVersion"','Status','UPDATE')
              AND has_column_privilege(current_user,'public."ProfileVersion"','Revision','UPDATE')
              AND has_column_privilege(current_user,'public."ProfileAssignment"','ProfileVersionId','UPDATE')
              AND has_column_privilege(current_user,'public."ProfileAssignment"','Revision','UPDATE')
              AND has_function_privilege(current_user,'public.taskmetric_lookup_installation(text,text,text,text)','EXECUTE')
              AND NOT has_table_privilege(current_user,'public."TaskMetricSchema"','INSERT,UPDATE,DELETE,TRUNCATE')
              AND NOT has_table_privilege(current_user,'public."ProjectRegistration"','INSERT,UPDATE,DELETE')
              AND NOT has_table_privilege(current_user,'public."TeamRegistration"','INSERT,UPDATE,DELETE')
              AND NOT has_table_privilege(current_user,'public."ProjectTeam"','INSERT,UPDATE,DELETE')
              AND NOT has_table_privilege(current_user,'public."TenantInstallation"','SELECT,INSERT,UPDATE,DELETE')
              AND NOT has_table_privilege(current_user,'public."TenantRegistration"','SELECT,INSERT,UPDATE,DELETE')
            """);
        Require.That(await columns.ExecuteScalarAsync(ct) is true, 503, "runtime_security_grants_required");
    }
}

/// <summary>Owns one connection/transaction. Its tenant capability must originate in verified Forge binding.</summary>
public sealed class TenantDatabaseSession : IAsyncDisposable
{
    private readonly NpgsqlConnection connection;
    private readonly NpgsqlTransaction transaction;
    public ConfigurationDbContext Db { get; }
    public TenantIdentity Identity { get; }
    private TenantDatabaseSession(NpgsqlConnection connection, NpgsqlTransaction transaction, ConfigurationDbContext db, TenantIdentity identity)
    { this.connection = connection; this.transaction = transaction; Db = db; Identity = identity; }

    public static async Task<TenantDatabaseSession> OpenAsync(string connectionString, AuthenticatedTenant tenant, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        RuntimeDatabase.ValidateSettings(connectionString);
        var connection = new NpgsqlConnection(connectionString);
        NpgsqlTransaction? transaction = null;
        ConfigurationDbContext? db = null;
        try
        {
            await connection.OpenAsync(ct);
            await RuntimeDatabase.ValidateRoleAsync(connection, ct);
            transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, ct);
            await using var bind = new NpgsqlCommand("SELECT set_config('taskmetric.tenant', @tenant, true)", connection, transaction);
            bind.Parameters.AddWithValue("tenant", tenant.Identity.TenantId);
            await bind.ExecuteNonQueryAsync(ct);
            db = new ConfigurationDbContext(new DbContextOptionsBuilder<ConfigurationDbContext>().UseNpgsql(connection).Options, tenant.Identity);
            await db.Database.UseTransactionAsync(transaction, ct);
            return new(connection, transaction, db, tenant.Identity);
        }
        catch
        {
            if (db is not null) await db.DisposeAsync();
            if (transaction is not null) await transaction.DisposeAsync();
            await connection.DisposeAsync();
            throw;
        }
    }
    public async Task CommitAsync(CancellationToken ct = default)
    {
        try { await transaction.CommitAsync(ct); }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation or
            PostgresErrorCodes.CheckViolation or PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
        { throw new OperationException(409, "configuration_transaction_conflict"); }
    }
    public async ValueTask DisposeAsync()
    {
        // Disposal rolls back an uncommitted transaction independently of the cancelled request token.
        try { await Db.DisposeAsync(); }
        finally { try { await transaction.DisposeAsync(); } finally { await connection.DisposeAsync(); } }
    }
}
