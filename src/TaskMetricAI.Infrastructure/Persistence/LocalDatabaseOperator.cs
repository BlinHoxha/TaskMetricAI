using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Infrastructure.Persistence;

public static class LocalDatabaseOperator
{
    /// <summary>Explicit trusted operator provisioning, never callable from the HTTP host.</summary>
    public static async Task RegisterScopesAsync(string setupConnection, TenantIdentity identity,
        string[] projects, string[] teams, (string Project, string Team)[] mappings, string reason, CancellationToken ct = default)
    {
        var settings = new NpgsqlConnectionStringBuilder(setupConnection);
        Require.That(settings.Database is "taskmetricai_dev" or "taskmetricai_test", 422, "isolated_database_required");
        Require.That(settings.Host is "localhost" or "127.0.0.1" or "::1", 422, "local_database_required");
        Require.Reason(reason);
        foreach (var id in projects.Concat(teams)) Require.Text(id, "scope_id_required");
        await using var connection = new NpgsqlConnection(setupConnection); await connection.OpenAsync(ct);
        Require.That(connection.UserName == "taskmetric_setup", 403, "operator_database_role_required");
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using (var bind = new NpgsqlCommand("SELECT set_config('taskmetric.tenant',@tenant,true)", connection, tx))
        { bind.Parameters.AddWithValue("tenant", identity.TenantId); await bind.ExecuteNonQueryAsync(ct); }
        await using var directory = new InstallationDbContext(new DbContextOptionsBuilder<InstallationDbContext>().UseNpgsql(connection).Options);
        await directory.Database.UseTransactionAsync(tx, ct);
        Require.That(await directory.Set<TenantRegistration>().AnyAsync(x => x.TenantId == identity.TenantId && x.OrganizationId == identity.OrganizationId, ct),
            403, "tenant_not_registered");
        await using var db = new ConfigurationDbContext(new DbContextOptionsBuilder<ConfigurationDbContext>().UseNpgsql(connection).Options, identity, operatorOnly: true);
        await db.Database.UseTransactionAsync(tx, ct);
        foreach (var id in projects.Distinct(StringComparer.Ordinal))
            if (!await db.Set<ProjectRegistration>().AnyAsync(x => x.Id == id, ct)) db.Add(new ProjectRegistration { TenantId = identity.TenantId, Id = id });
        foreach (var id in teams.Distinct(StringComparer.Ordinal))
            if (!await db.Set<TeamRegistration>().AnyAsync(x => x.Id == id, ct)) db.Add(new TeamRegistration { TenantId = identity.TenantId, Id = id });
        foreach (var mapping in mappings.Distinct())
            if (!await db.Set<ProjectTeam>().AnyAsync(x => x.ProjectId == mapping.Project && x.TeamId == mapping.Team, ct))
                db.Add(new ProjectTeam { TenantId = identity.TenantId, ProjectId = mapping.Project, TeamId = mapping.Team });
        db.Add(new AuditEvent { TenantId = identity.TenantId, ActorId = "taskmetric_setup", TargetType = "scope_registration",
            Operation = "scopes.registered", SubjectId = identity.TenantId, Reason = reason, OccurredAt = DateTimeOffset.UtcNow,
            AfterStateJson = Documents.Write(new { Projects = projects, Teams = teams, Mappings = mappings.Select(x => new { x.Project, x.Team }) }) });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    /// <summary>Provision only named local TaskMetric databases using an explicitly supplied development role.</summary>
    public static async Task CreateAndInitializeAsync(string developmentConnection, CancellationToken ct = default)
    {
        var target = new NpgsqlConnectionStringBuilder(developmentConnection);
        Require.That(target.Database is "taskmetricai_dev" or "taskmetricai_test", 422, "isolated_database_required");
        Require.That(target.Host is "localhost" or "127.0.0.1" or "::1", 422, "local_database_required");
        // Existing isolated databases do not require a maintenance-database passfile entry.
        try
        {
            await using var existing = new NpgsqlConnection(developmentConnection); await existing.OpenAsync(ct);
            await using var ownerCheck = new NpgsqlCommand("SELECT pg_get_userbyid(datdba) = current_user FROM pg_database WHERE datname = current_database()", existing);
            Require.That(await ownerCheck.ExecuteScalarAsync(ct) is true, 403, "isolated_database_owner_mismatch");
            Require.That(existing.PostgreSqlVersion.Major == 17, 422, "postgresql_17_required");
            await PostgreSqlSchema.InitializeLocalAsync(developmentConnection, ct);
            await PostgreSqlHardening.ApplyAsync(developmentConnection, ct);
            return;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName) { }
        var admin = new NpgsqlConnectionStringBuilder(developmentConnection) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(admin.ConnectionString); await connection.OpenAsync(ct);
        Require.That(connection.PostgreSqlVersion.Major == 17, 422, "postgresql_17_required");
        await using var role = new NpgsqlCommand("SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user", connection);
        Require.That(await role.ExecuteScalarAsync(ct) is false, 403, "development_role_required");
        await using var exists = new NpgsqlCommand("SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname = @database", connection);
        exists.Parameters.AddWithValue("database", target.Database!);
        var owner = await exists.ExecuteScalarAsync(ct);
        if (owner is null)
        {
            // Database identifier is restricted to the two literals above; no arbitrary object names.
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{target.Database}\"", connection);
            await create.ExecuteNonQueryAsync(ct);
        }
        else
        {
            await using var current = new NpgsqlCommand("SELECT current_user", connection);
            Require.That(Equals(owner, await current.ExecuteScalarAsync(ct)), 403, "isolated_database_owner_mismatch");
        }
        await PostgreSqlSchema.InitializeLocalAsync(developmentConnection, ct);
        await PostgreSqlHardening.ApplyAsync(developmentConnection, ct);
    }

    /// <summary>Operator bootstrap is not a public registration endpoint. No grants are inferred from caller claims.</summary>
    public static async Task RegisterInstallationAsync(string connectionString, TenantInstallation binding,
        string administratorActor, string reason, CancellationToken ct = default)
    {
        await PostgreSqlSchema.InitializeLocalAsync(connectionString, ct);
        Require.Reason(reason);
        var identity = new TenantIdentity(binding.TenantId, binding.OrganizationId, administratorActor,
            binding.CloudId, binding.InstallationId, binding.AppId, binding.EnvironmentId);
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var tenantCommand = new NpgsqlCommand("SELECT set_config('taskmetric.tenant', @tenant, true)", connection);
        tenantCommand.Parameters.AddWithValue("tenant", identity.TenantId); await tenantCommand.ExecuteNonQueryAsync(ct);
        await using var directory = new InstallationDbContext(new DbContextOptionsBuilder<InstallationDbContext>().UseNpgsql(connection).Options);
        await directory.Database.UseTransactionAsync(transaction, ct);
        var registration = await directory.Set<TenantRegistration>().SingleOrDefaultAsync(x => x.TenantId == binding.TenantId, ct);
        Require.That(registration is null || registration.OrganizationId == binding.OrganizationId, 409, "tenant_organization_mismatch");
        if (registration is null) directory.Add(new TenantRegistration { TenantId = binding.TenantId, OrganizationId = binding.OrganizationId });
        Require.That(!await directory.Set<TenantInstallation>().AnyAsync(x => x.TenantId == binding.TenantId && x.OrganizationId != binding.OrganizationId, ct),
            409, "tenant_organization_mismatch");
        directory.Add(binding); await directory.SaveChangesAsync(ct);
        await using var db = new ConfigurationDbContext(new DbContextOptionsBuilder<ConfigurationDbContext>().UseNpgsql(connection).Options, identity, operatorOnly: true);
        await db.Database.UseTransactionAsync(transaction, ct);
        db.Add(new ScopeGrant { TenantId = identity.TenantId, ActorId = administratorActor, Role = AccessRole.SystemAdministrator,
            ScopeType = ScopeType.Organization, ScopeId = identity.OrganizationId });
        db.Add(new AuditEvent { TenantId = identity.TenantId, ActorId = administratorActor, Operation = "installation.registered",
            SubjectId = binding.InstallationId, Reason = reason, OccurredAt = DateTimeOffset.UtcNow,
            TargetType = "installation", TargetActorId = administratorActor, ScopeType = ScopeType.Organization, ScopeId = identity.OrganizationId,
            AfterStateJson = Documents.Write(new { binding.AppId, binding.EnvironmentId, binding.InstallationId, binding.CloudId,
                binding.OrganizationId, binding.Enabled, Administrator = administratorActor, Role = AccessRole.SystemAdministrator }) });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
}
