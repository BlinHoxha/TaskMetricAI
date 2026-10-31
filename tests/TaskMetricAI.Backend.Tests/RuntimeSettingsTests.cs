using Npgsql;
using TaskMetricAI.Domain;
using TaskMetricAI.Infrastructure.Persistence;

namespace TaskMetricAI.Backend.Tests;

public sealed class RuntimeSettingsTests
{
    [Theory]
    [InlineData("Username=taskmetric_setup;Database=taskmetricai_test")]
    [InlineData("Username=taskmetric_runtime_test;Database=taskmetricai_dev")]
    [InlineData("Username=taskmetric_runtime_test;Database=taskmetricai_test;No Reset On Close=true")]
    [InlineData("Username=taskmetric_runtime_test;Database=taskmetricai_test;Multiplexing=true")]
    [InlineData("Username=taskmetric_runtime_test;Database=taskmetricai_test;Include Error Detail=true")]
    public void Unsafe_or_operator_connections_fail_before_network_access(string connection) =>
        Assert.Throws<OperationException>(() => RuntimeDatabase.ValidateSettings(new NpgsqlConnectionStringBuilder("Host=localhost;" + connection).ConnectionString));
}
