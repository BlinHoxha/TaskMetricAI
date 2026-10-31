using Npgsql;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Infrastructure.Persistence;

/// <summary>Explicit local migrations and reviewed runtime grants, run only by the schema owner.</summary>
public static class PostgreSqlHardening
{
    public static Task ApplyAsync(string setupConnection, CancellationToken ct = default) => ApplyAsync(setupConnection, [], ct);

    public static async Task ApplyAsync(string setupConnection, IReadOnlyList<MigrationScopeRegistration> registrations, CancellationToken ct = default)
    {
        var settings = new NpgsqlConnectionStringBuilder(setupConnection);
        Require.That(settings.Database is "taskmetricai_dev" or "taskmetricai_test", 422, "isolated_database_required");
        Require.That(settings.Host is "localhost" or "127.0.0.1" or "::1", 422, "local_database_required");
        await using var connection = new NpgsqlConnection(setupConnection); await connection.OpenAsync(ct);
        Require.That(connection.UserName == "taskmetric_setup", 403, "operator_database_role_required");
        await using var tx = await connection.BeginTransactionAsync(ct);
        await ApplyWithinTransactionAsync(connection, tx, registrations, "public", ct);
        await tx.CommitAsync(ct);
    }

    // Tests execute the actual plan in a transaction-local isolated schema, never reset public or a database.
    internal static async Task ApplyWithinTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction tx,
        IReadOnlyList<MigrationScopeRegistration> registrations, string schema, CancellationToken ct = default)
    {
        Require.That(schema == "public" || System.Text.RegularExpressions.Regex.IsMatch(schema, "^migration_test_[a-f0-9]{32}$"), 422, "migration_schema_invalid");
        Require.That(connection.UserName == "taskmetric_setup", 403, "operator_database_role_required");
        string Plan(string sql) => sql.Replace("public.", $"\"{schema}\".", StringComparison.Ordinal)
            .Replace("'public'", $"'{schema}'", StringComparison.Ordinal).Replace("SCHEMA public", $"SCHEMA \"{schema}\"", StringComparison.Ordinal);
        async Task Execute(string sql)
        { await using var command = new NpgsqlCommand(sql, connection, tx); await command.ExecuteNonQueryAsync(ct); }
        await Execute($"SET LOCAL search_path = \"{schema}\", pg_catalog");
        await Execute(Plan(Migration));
        foreach (var registration in registrations.Distinct())
        {
            Require.Text(registration.TenantId, "migration_tenant_required");
            Require.Text(registration.ScopeId, "migration_scope_required");
            Require.That(registration.ScopeType is ScopeType.Team or ScopeType.JiraProject, 422, "migration_scope_invalid");
            await using var registered = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM \"TenantRegistration\" WHERE \"TenantId\"=@tenant)", connection, tx);
            registered.Parameters.AddWithValue("tenant", registration.TenantId);
            Require.That(await registered.ExecuteScalarAsync(ct) is true, 409, "migration_tenant_not_registered");
            var table = registration.ScopeType == ScopeType.Team ? "TeamRegistration" : "ProjectRegistration";
            await using var insert = new NpgsqlCommand($"SELECT set_config('taskmetric.tenant',@tenant,true); INSERT INTO \"{table}\" (\"TenantId\",\"Id\") VALUES (@tenant,@id) ON CONFLICT DO NOTHING", connection, tx);
            insert.Parameters.AddWithValue("tenant", registration.TenantId); insert.Parameters.AddWithValue("id", registration.ScopeId);
            await insert.ExecuteNonQueryAsync(ct);
        }
        // Check every tenant's mappings, not only the operator's current RLS setting. Never infer entitlements.
        await using var tenants = new NpgsqlCommand("SELECT \"TenantId\" FROM \"TenantRegistration\"", connection, tx);
        var tenantIds = new List<string>();
        await using (var reader = await tenants.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct)) tenantIds.Add(reader.GetString(0));
        foreach (var tenant in tenantIds)
        {
            await using var missing = new NpgsqlCommand("SELECT set_config('taskmetric.tenant',@tenant,true)", connection, tx);
            missing.Parameters.AddWithValue("tenant", tenant);
            await missing.ExecuteNonQueryAsync(ct);
            missing.CommandText = "SELECT EXISTS(SELECT 1 FROM \"ProjectTeam\" m WHERE NOT EXISTS(SELECT 1 FROM \"ProjectRegistration\" p WHERE p.\"TenantId\"=m.\"TenantId\" AND p.\"Id\"=m.\"ProjectId\") OR NOT EXISTS(SELECT 1 FROM \"TeamRegistration\" t WHERE t.\"TenantId\"=m.\"TenantId\" AND t.\"Id\"=m.\"TeamId\"))";
            Require.That(await missing.ExecuteScalarAsync(ct) is false, 409, "migration_scope_manifest_required");
        }
        await Execute(Plan(Completion));
    }

    public static Task GrantRuntimeAsync(string setupConnection, CancellationToken ct = default) => GrantRuntimeAsync(setupConnection, [], ct);

    public static async Task GrantRuntimeAsync(string setupConnection, IReadOnlyList<MigrationScopeRegistration> registrations, CancellationToken ct = default)
    {
        var settings = new NpgsqlConnectionStringBuilder(setupConnection);
        var role = settings.Database switch { "taskmetricai_dev" => "taskmetric_runtime_dev", "taskmetricai_test" => "taskmetric_runtime_test",
            _ => throw new OperationException(422, "isolated_database_required") };
        await ApplyAsync(setupConnection, registrations, ct);
        await using var connection = new NpgsqlConnection(setupConnection); await connection.OpenAsync(ct);
        await using var probe = new NpgsqlCommand("SELECT NOT (rolsuper OR rolcreatedb OR rolcreaterole OR rolbypassrls OR rolreplication) AND rolcanlogin FROM pg_roles WHERE rolname=@role", connection);
        probe.Parameters.AddWithValue("role", role);
        Require.That(await probe.ExecuteScalarAsync(ct) is true, 422, "safe_runtime_role_must_exist");
        await using var tx = await connection.BeginTransactionAsync(ct);
        var sql = $"""
            REVOKE ALL ON DATABASE "{settings.Database}" FROM PUBLIC;
            GRANT CONNECT ON DATABASE "{settings.Database}" TO "{role}";
            REVOKE CREATE ON SCHEMA public FROM PUBLIC;
            REVOKE ALL ON SCHEMA public FROM "{role}";
            GRANT USAGE ON SCHEMA public TO "{role}";
            REVOKE ALL ON ALL TABLES IN SCHEMA public FROM "{role}";
            REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM "{role}";
            REVOKE ALL ON ALL FUNCTIONS IN SCHEMA public FROM "{role}";
            GRANT SELECT, INSERT ON "MetricVersion", "CatalogVersion", "CatalogEntry", "AuditEvent", "ProfileVersion", "ScopeGrant", "ProfileAssignment" TO "{role}";
            GRANT UPDATE ("Status", "Revision") ON "ProfileVersion" TO "{role}";
            GRANT UPDATE ("ProfileVersionId", "Revision"), DELETE ON "ProfileAssignment" TO "{role}";
            GRANT SELECT ON "ProjectRegistration", "TeamRegistration", "ProjectTeam" TO "{role}";
            GRANT SELECT ON "TaskMetricSchema" TO "{role}";
            GRANT DELETE ON "ScopeGrant" TO "{role}";
            GRANT EXECUTE ON FUNCTION public.taskmetric_lookup_installation(text,text,text,text) TO "{role}";
            """;
        await using var grant = new NpgsqlCommand(sql, connection, tx); await grant.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }

    private const string Migration = """
        SELECT pg_advisory_xact_lock(761742013);
        DO $$ BEGIN
          IF (SELECT "Version" FROM "TaskMetricSchema") NOT IN (1,2,3) THEN
            RAISE EXCEPTION 'unsupported schema' USING ERRCODE='23514';
          END IF;
        END $$;
        CREATE TABLE IF NOT EXISTS "TenantRegistration" (
          "TenantId" text PRIMARY KEY, "OrganizationId" text NOT NULL,
          CONSTRAINT "AK_TenantRegistration_TenantId_OrganizationId" UNIQUE ("TenantId","OrganizationId"));
        INSERT INTO "TenantRegistration" ("TenantId","OrganizationId")
          SELECT DISTINCT "TenantId","OrganizationId" FROM "TenantInstallation"
          ON CONFLICT ("TenantId") DO NOTHING;
        DO $$ BEGIN
          IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='FK_Installation_TenantRegistration') THEN
            ALTER TABLE "TenantInstallation" ADD CONSTRAINT "FK_Installation_TenantRegistration"
              FOREIGN KEY ("TenantId","OrganizationId") REFERENCES "TenantRegistration" ("TenantId","OrganizationId") ON DELETE RESTRICT;
          END IF;
        END $$;
        ALTER TABLE "AuditEvent" ADD COLUMN IF NOT EXISTS "TargetType" text NOT NULL DEFAULT 'legacy';
        ALTER TABLE "AuditEvent" ADD COLUMN IF NOT EXISTS "TargetVersion" integer;
        ALTER TABLE "AuditEvent" ADD COLUMN IF NOT EXISTS "TargetActorId" text;
        ALTER TABLE "AuditEvent" ADD COLUMN IF NOT EXISTS "ScopeType" integer;
        ALTER TABLE "AuditEvent" ADD COLUMN IF NOT EXISTS "ScopeId" text;
        ALTER TABLE "AuditEvent" ADD COLUMN IF NOT EXISTS "BeforeStateJson" text;
        ALTER TABLE "AuditEvent" ADD COLUMN IF NOT EXISTS "AfterStateJson" text;
        DO $$ BEGIN
          IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='CK_ScopeGrant_ValidRoleScope') THEN
            ALTER TABLE "ScopeGrant" ADD CONSTRAINT "CK_ScopeGrant_ValidRoleScope"
              CHECK ("Role" BETWEEN 0 AND 3 AND "ScopeType" BETWEEN 0 AND 2 AND ("Role"<>3 OR "ScopeType"=0));
          END IF;
        END $$;
        CREATE OR REPLACE FUNCTION public.taskmetric_profile_insert_guard() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog AS $$
        BEGIN
          IF NEW."Status"<>0 OR NEW."Revision"<>0 THEN
            RAISE EXCEPTION 'profile must start as draft' USING ERRCODE='23514';
          END IF;
          RETURN NEW;
        END $$;
        DROP TRIGGER IF EXISTS profile_insert_guard ON "ProfileVersion";
        CREATE TRIGGER profile_insert_guard BEFORE INSERT ON "ProfileVersion" FOR EACH ROW EXECUTE FUNCTION public.taskmetric_profile_insert_guard();
        CREATE OR REPLACE FUNCTION public.taskmetric_assignment_guard() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog AS $$
        DECLARE p public."ProfileVersion"%ROWTYPE;
        BEGIN
          SELECT * INTO p FROM public."ProfileVersion" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."ProfileVersionId" FOR UPDATE;
          IF NOT FOUND THEN RETURN NEW; END IF;
          IF p."Status"<>2 OR p."ScopeType"<>NEW."ScopeType" OR p."ScopeId"<>NEW."ScopeId" THEN
            RAISE EXCEPTION 'assignment requires active same-scope profile' USING ERRCODE='23514';
          END IF;
          RETURN NEW;
        END $$;
        DROP TRIGGER IF EXISTS assignment_guard ON "ProfileAssignment";
        CREATE TRIGGER assignment_guard BEFORE INSERT OR UPDATE ON "ProfileAssignment" FOR EACH ROW EXECUTE FUNCTION public.taskmetric_assignment_guard();
        CREATE OR REPLACE FUNCTION public.taskmetric_archive_assignments() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog AS $$
        BEGIN
          IF NEW."Status"=3 THEN
            DELETE FROM public."ProfileAssignment" WHERE "TenantId"=NEW."TenantId" AND "ProfileVersionId"=NEW."Id";
          END IF;
          RETURN NEW;
        END $$;
        DROP TRIGGER IF EXISTS archive_assignments ON "ProfileVersion";
        CREATE TRIGGER archive_assignments AFTER UPDATE ON "ProfileVersion" FOR EACH ROW EXECUTE FUNCTION public.taskmetric_archive_assignments();
        CREATE OR REPLACE FUNCTION public.taskmetric_catalog_entry_guard() RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog AS $$
        BEGIN
          IF NOT EXISTS (SELECT 1 FROM public."MetricVersion" WHERE "TenantId"=NEW."TenantId" AND "Id"=NEW."MetricVersionId" AND "MetricId"=NEW."MetricId") THEN
            RAISE EXCEPTION 'catalog metric identity mismatch' USING ERRCODE='23514';
          END IF;
          RETURN NEW;
        END $$;
        DROP TRIGGER IF EXISTS catalog_entry_guard ON "CatalogEntry";
        CREATE TRIGGER catalog_entry_guard BEFORE INSERT ON "CatalogEntry" FOR EACH ROW EXECUTE FUNCTION public.taskmetric_catalog_entry_guard();
        CREATE OR REPLACE FUNCTION public.taskmetric_lookup_installation(app text, env text, installation text, cloud text)
        RETURNS TABLE ("TenantId" text,"OrganizationId" text) LANGUAGE sql SECURITY DEFINER STABLE SET search_path=pg_catalog AS $$
          SELECT i."TenantId",i."OrganizationId" FROM public."TenantInstallation" i
          JOIN public."TenantRegistration" t ON t."TenantId"=i."TenantId" AND t."OrganizationId"=i."OrganizationId"
          WHERE i."Enabled" AND i."AppId"=app AND i."EnvironmentId"=env AND i."InstallationId"=installation AND i."CloudId"=cloud;
        $$;
        REVOKE EXECUTE ON ALL FUNCTIONS IN SCHEMA public FROM PUBLIC;
        ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;
        ALTER TABLE "TaskMetricSchema" DROP CONSTRAINT IF EXISTS "TaskMetricSchema_Version_check";
        CREATE TABLE IF NOT EXISTS "ProjectRegistration" ("TenantId" varchar(200) NOT NULL, "Id" varchar(200) NOT NULL, PRIMARY KEY ("TenantId","Id"));
        CREATE TABLE IF NOT EXISTS "TeamRegistration" ("TenantId" varchar(200) NOT NULL, "Id" varchar(200) NOT NULL, PRIMARY KEY ("TenantId","Id"));
        ALTER TABLE "ProjectRegistration" ENABLE ROW LEVEL SECURITY;
        ALTER TABLE "ProjectRegistration" FORCE ROW LEVEL SECURITY;
        ALTER TABLE "TeamRegistration" ENABLE ROW LEVEL SECURITY;
        ALTER TABLE "TeamRegistration" FORCE ROW LEVEL SECURITY;
        DO $$ BEGIN
          IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname='public' AND tablename='ProjectRegistration' AND policyname='tenant_isolation') THEN
            CREATE POLICY tenant_isolation ON "ProjectRegistration" USING ("TenantId"=current_setting('taskmetric.tenant',true)) WITH CHECK ("TenantId"=current_setting('taskmetric.tenant',true));
          END IF;
          IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname='public' AND tablename='TeamRegistration' AND policyname='tenant_isolation') THEN
            CREATE POLICY tenant_isolation ON "TeamRegistration" USING ("TenantId"=current_setting('taskmetric.tenant',true)) WITH CHECK ("TenantId"=current_setting('taskmetric.tenant',true));
          END IF;
        END $$;
        """;

    private const string Completion = """
        DO $$ BEGIN
          IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='FK_ProjectTeam_ProjectRegistration_TenantId_ProjectId' AND connamespace='public'::regnamespace) THEN
            ALTER TABLE "ProjectTeam" ADD CONSTRAINT "FK_ProjectTeam_ProjectRegistration_TenantId_ProjectId" FOREIGN KEY ("TenantId","ProjectId") REFERENCES "ProjectRegistration" ("TenantId","Id") ON DELETE RESTRICT;
          END IF;
          IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='FK_ProjectTeam_TeamRegistration_TenantId_TeamId' AND connamespace='public'::regnamespace) THEN
            ALTER TABLE "ProjectTeam" ADD CONSTRAINT "FK_ProjectTeam_TeamRegistration_TenantId_TeamId" FOREIGN KEY ("TenantId","TeamId") REFERENCES "TeamRegistration" ("TenantId","Id") ON DELETE RESTRICT;
          END IF;
        END $$;
        UPDATE "TaskMetricSchema" SET "Version"=3 WHERE "Version" IN (1,2);
        """;
}

public sealed record MigrationScopeRegistration(string TenantId, ScopeType ScopeType, string ScopeId);
