using Npgsql;
using TaskMetricAI.Domain;
using TaskMetricAI.Infrastructure.Authentication;
using TaskMetricAI.Infrastructure.Persistence;

namespace TaskMetricAI.Api;

public sealed class RuntimeStartupGuard(RuntimeConnection runtime, ForgeOptions forge) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        Require.That(!string.IsNullOrWhiteSpace(forge.AppId) && forge.AllowedEnvironments.Length > 0,
            503, "forge_not_configured");
        await using var connection = new NpgsqlConnection(runtime.Value); await connection.OpenAsync(ct);
        await RuntimeDatabase.ValidateRoleAsync(connection, ct);
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
