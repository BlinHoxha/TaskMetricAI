using Microsoft.EntityFrameworkCore;
using TaskMetricAI.Domain;
using TaskMetricAI.Infrastructure.Persistence;

namespace TaskMetricAI.Backend.Tests;

public sealed class PostgreSqlModelTests
{
    [Fact]
    public void Every_configuration_entity_has_a_tenant_filter_and_composite_key_and_relationships()
    {
        using var db = new ConfigurationDbContext(new DbContextOptionsBuilder<ConfigurationDbContext>()
            .UseNpgsql("Host=localhost;Database=taskmetricai_test;Username=model_only").Options, AuthorizationTests.Identity());
        foreach (var entity in db.Model.GetEntityTypes())
        {
            Assert.NotNull(entity.GetDeclaredQueryFilters().SingleOrDefault());
            Assert.Equal(new[] { "TenantId", "Id" }, entity.FindPrimaryKey()!.Properties.Select(x => x.Name));
            foreach (var key in entity.GetForeignKeys()) Assert.Equal("TenantId", key.Properties.First().Name);
        }
        var sql = db.Database.GenerateCreateScript();
        Assert.Contains("CREATE TABLE \"CatalogEntry\"", sql);
        Assert.Contains("WHERE \"Status\" = 2", sql);
        Assert.Contains("FOREIGN KEY (\"TenantId\", \"ProfileVersionId\")", sql);
        Assert.Contains("FOREIGN KEY (\"TenantId\", \"MetricVersionId\")", sql);
    }

    [Theory]
    [InlineData("production", "localhost")]
    [InlineData("taskmetricai_test", "remote.example")]
    public async Task Local_schema_operator_rejects_unapproved_database_or_host_before_connecting(string database, string host) =>
        await Assert.ThrowsAsync<OperationException>(() => PostgreSqlSchema.InitializeLocalAsync($"Host={host};Database={database};Username=unused"));
}
