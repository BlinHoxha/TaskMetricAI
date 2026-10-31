using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using TaskMetricAI.Domain;

namespace TaskMetricAI.Infrastructure.Persistence;

/// <summary>Check every EF command, including commands on already-open connections. Never repair a mismatched tenant.</summary>
internal sealed class TenantCommandGuard(string tenantId, bool operatorOnly) : DbCommandInterceptor
{
    private DbCommand Probe(DbCommand command)
    {
        var connection = (NpgsqlConnection)command.Connection!;
        if (operatorOnly) Require.That(connection.UserName == "taskmetric_setup", 403, "operator_database_role_required");
        else Require.That(command.Transaction is not null, 401, "tenant_transaction_required");
        var probe = connection.CreateCommand();
        probe.Transaction = (NpgsqlTransaction?)command.Transaction;
        probe.CommandText = "SELECT current_setting('taskmetric.tenant', true)";
        return probe;
    }
    private void Check(DbCommand command)
    {
        if (command.Connection is not NpgsqlConnection) return;
        using var probe = Probe(command);
        Require.That(Equals(probe.ExecuteScalar(), tenantId), 403, "tenant_session_mismatch");
    }
    private async Task CheckAsync(DbCommand command, CancellationToken ct)
    {
        if (command.Connection is not NpgsqlConnection) return;
        await using var probe = Probe(command);
        Require.That(Equals(await probe.ExecuteScalarAsync(ct), tenantId), 403, "tenant_session_mismatch");
    }
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    { Check(command); return result; }
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { await CheckAsync(command, cancellationToken); return result; }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    { Check(command); return result; }
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { await CheckAsync(command, cancellationToken); return result; }
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    { Check(command); return result; }
    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<object> result, CancellationToken cancellationToken = default)
    { await CheckAsync(command, cancellationToken); return result; }
}
