using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;
using TaskMetricAI.Infrastructure.Persistence;

namespace TaskMetricAI.Backend.Tests;

public sealed class ConfigurationWorkflowTests
{
    [Fact]
    public async Task Custom_catalog_and_profile_versions_preserve_history_and_record_audit()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ConfigurationDbContext>().UseSqlite(connection).Options;
        await using var db = new ConfigurationDbContext(options, AuthorizationTests.Identity());
        await db.Database.EnsureCreatedAsync(); db.Add(PersistenceTests.Grant("a")); await db.SaveChangesAsync();
        var service = new ConfigurationService(new TenantRepository(db), AuthorizationTests.Identity(), TimeProvider.System);
        var first = await service.AddMetricVersionAsync("custom", new("Custom", "Original", new("1", "2", "3", "4", "5"), "create"), 0);
        await service.AddCatalogVersionAsync("catalog", [first.Reference], 0, "select");
        var next = await service.AddMetricVersionAsync("custom", new("Changed", "Revision", new("a", "b", "c", "d", "e"), "revise"), 1);
        await service.AddCatalogVersionAsync("catalog", [next.Reference], 1, "select revision");
        Assert.Equal("Original", (await service.ReadCatalogAsync("catalog", 1)).GetRequired("custom").Description);
        Assert.Equal("Revision", (await service.ReadCatalogAsync("catalog", 2)).GetRequired("custom").Description);
        var input = PersistenceTests.Input() with { Criteria = [CriterionDefinition.FromMetric(first, 1m)] };
        var profile = await service.AddProfileVersionAsync("profile", input, 0);
        await service.TransitionAsync("profile", 1, ProfileStatus.Validated, 0, "validate");
        await service.TransitionAsync("profile", 1, ProfileStatus.Active, 1, "approve");
        await service.AssignAsync(input.Scope, "profile", 1, "assign");
        var revised = await service.AddProfileVersionAsync("profile", input with { Description = "next version" }, 1);
        Assert.Equal(2, revised.Version);
        Assert.Equal(profile.ContentHash, (await service.ReadProfileAsync("profile", 1)).ContentHash);
        Assert.Single(await db.Set<ProfileAssignment>().ToArrayAsync());
        Assert.Equal(9, await db.Set<AuditEvent>().CountAsync());
        await Assert.ThrowsAsync<OperationException>(() => service.AddProfileVersionAsync("profile", input, 0));
        await Assert.ThrowsAsync<OperationException>(() => service.AddCatalogVersionAsync("bad", [new("b", "custom", 1)], 0, "reject"));
        var falseSource = input with { Criteria = [CriterionDefinition.FromMetric(first, 1m) with { Label = "forged" }] };
        await Assert.ThrowsAsync<OperationException>(() => service.AddProfileVersionAsync("bad", falseSource, 0));
    }

    [Fact]
    public async Task Manager_cannot_edit_catalog_delegate_roles_or_manage_other_profile_scope()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ConfigurationDbContext>().UseSqlite(connection).Options;
        await using var db = new ConfigurationDbContext(options, AuthorizationTests.Identity());
        await db.Database.EnsureCreatedAsync();
        db.Add(PersistenceTests.Grant("a", role: AccessRole.EvaluationManager, scope: ScopeType.Team, scopeId: "team"));
        db.Add(new TeamRegistration { TenantId = "a", Id = "team" });
        await db.SaveChangesAsync();
        var service = new ConfigurationService(new TenantRepository(db), AuthorizationTests.Identity(), TimeProvider.System);
        await service.AddProfileVersionAsync("team-profile", PersistenceTests.Input(scope: new(ScopeType.Team, "team")), 0);
        await Assert.ThrowsAsync<OperationException>(() => service.AddProfileVersionAsync("other", PersistenceTests.Input(), 0));
        await Assert.ThrowsAsync<OperationException>(() => service.GrantAsync("self", AccessRole.SystemAdministrator, new(ScopeType.Organization, "org"), "escalate"));
        await Assert.ThrowsAsync<OperationException>(() => service.AddMetricVersionAsync("metric", new("m", "d", new("1", "2", "3", "4", "5"), "no"), 0));
        Assert.Single(await db.Set<ProfileVersion>().ToArrayAsync());
        Assert.Single(await db.Set<AuditEvent>().ToArrayAsync());
    }

    [Fact]
    public async Task Installation_directory_requires_all_verified_fields_and_enabled_registration()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new InstallationDbContext(new DbContextOptionsBuilder<InstallationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var row = new TenantInstallation { TenantId = "a", OrganizationId = "org", AppId = "app", EnvironmentId = "env", InstallationId = "install", CloudId = "cloud" };
        db.Add(new TenantRegistration { TenantId = "a", OrganizationId = "org" });
        db.Add(row); await db.SaveChangesAsync(); var directory = new InstallationDirectory(db);
        var verified = new VerifiedForgeIdentity("app", "env", "DEVELOPMENT", "install", "cloud", "actor");
        Assert.Equal("a", (await directory.ResolveAsync(verified))!.TenantId);
        foreach (var mismatch in new[] { verified with { AppId = "other" }, verified with { EnvironmentId = "other" },
            verified with { InstallationId = "other" }, verified with { CloudId = "other" } }) Assert.Null(await directory.ResolveAsync(mismatch));
        row.Enabled = false; await db.SaveChangesAsync(); Assert.Null(await directory.ResolveAsync(verified));
    }
}
