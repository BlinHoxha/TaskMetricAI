using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;
using TaskMetricAI.Infrastructure.Persistence;

namespace TaskMetricAI.Backend.Tests;

[Trait("Category", "PostgreSQL")]
public sealed class RelationshipHardeningTests
{
    [Fact]
    public async Task Catalog_entries_cannot_lie_about_metric_identity()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await using (var create = await fixture.Session(fixture.A))
        {
            var metric = await fixture.Service(create).AddMetricVersionAsync("real", new("M", "D", new("1","2","3","4","5"), "create"), 0);
            await fixture.Service(create).AddCatalogVersionAsync("catalog", [metric.Reference], 0, "select");
            await create.CommitAsync();
        }
        await using var attempt = await fixture.Session(fixture.A);
        var row = await attempt.Db.Set<MetricVersion>().SingleAsync();
        var catalog = await attempt.Db.Set<CatalogVersion>().SingleAsync();
        attempt.Db.Add(new CatalogEntry { TenantId = fixture.A, CatalogVersionId = catalog.Id, MetricVersionId = row.Id, MetricId = "fabricated" });
        var rejected = await Assert.ThrowsAsync<DbUpdateException>(() => attempt.Db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(rejected.InnerException).SqlState);
    }

    [Fact]
    public async Task Installation_cannot_change_organization_for_an_existing_tenant()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await using var directory = new InstallationDbContext(new DbContextOptionsBuilder<InstallationDbContext>().UseNpgsql(fixture.Setup).Options);
        directory.Add(new TenantInstallation { TenantId = fixture.A, OrganizationId = "foreign-org", AppId = fixture.AppId,
            EnvironmentId = "other", InstallationId = "other", CloudId = "11111111-1111-1111-1111-111111111111" });
        var rejected = await Assert.ThrowsAsync<DbUpdateException>(() => directory.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(rejected.InnerException).SqlState);
    }

    [Fact]
    public async Task Assignment_requires_active_same_scope_profile_in_database_not_only_application_code()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await using var session = await fixture.Session(fixture.A);
        var row = await session.Db.Set<ProfileVersion>().SingleAsync();
        session.Db.Add(new ProfileAssignment { TenantId = fixture.A, ProfileVersionId = row.Id,
            ScopeType = ScopeType.Organization, ScopeId = "org" });
        var rejected = await Assert.ThrowsAsync<DbUpdateException>(() => session.Db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, Assert.IsType<PostgresException>(rejected.InnerException).SqlState);
    }
}
