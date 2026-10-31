using Npgsql;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;

namespace TaskMetricAI.Infrastructure.Persistence;

public sealed class RuntimeInstallationDirectory(string connectionString) : IInstallationDirectory
{
    public async Task<TenantIdentity?> ResolveAsync(VerifiedForgeIdentity identity, CancellationToken cancellationToken = default)
    {
        RuntimeDatabase.ValidateSettings(connectionString);
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(cancellationToken);
        await RuntimeDatabase.ValidateRoleAsync(connection, cancellationToken);
        await using var command = new NpgsqlCommand("SELECT * FROM public.taskmetric_lookup_installation(@app,@env,@install,@cloud)", connection);
        command.Parameters.AddWithValue("app", identity.AppId); command.Parameters.AddWithValue("env", identity.EnvironmentId);
        command.Parameters.AddWithValue("install", identity.InstallationId); command.Parameters.AddWithValue("cloud", identity.CloudId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new(reader.GetString(0), reader.GetString(1), identity.ActorId, identity.CloudId,
            identity.InstallationId, identity.AppId, identity.EnvironmentId);
    }
}
