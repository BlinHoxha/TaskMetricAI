using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using TaskMetricAI.Domain;

namespace TaskMetricAI.Infrastructure.Persistence;

/// <summary>Set the RLS tenant on every opened PostgreSQL connection, including pooled connections.</summary>
internal sealed class TenantSessionInterceptor(string tenantId) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (connection is not NpgsqlConnection) return;
        using var command = Command(connection); command.ExecuteNonQuery();
    }
    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (connection is not NpgsqlConnection) return;
        await using var command = Command(connection); await command.ExecuteNonQueryAsync(cancellationToken);
    }
    private DbCommand Command(DbConnection connection)
    {
        Require.That(((NpgsqlConnection)connection).UserName == "taskmetric_setup", 403, "operator_database_role_required");
        var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('taskmetric.tenant', @tenant, false)";
        command.Parameters.Add(new NpgsqlParameter("tenant", tenantId));
        return command;
    }
}
