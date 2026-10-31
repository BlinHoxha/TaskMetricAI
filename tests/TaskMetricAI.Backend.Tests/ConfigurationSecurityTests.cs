using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;
using TaskMetricAI.Infrastructure.Persistence;

namespace TaskMetricAI.Backend.Tests;

public sealed class ConfigurationSecurityTests
{
    [Fact]
    public async Task Weights_disabled_metrics_and_historical_versions_roundtrip_without_catalog_lookup()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ConfigurationDbContext>().UseSqlite(connection).Options;
        await using var db = new ConfigurationDbContext(options, AuthorizationTests.Identity());
        await db.Database.EnsureCreatedAsync(); db.Add(PersistenceTests.Grant("a")); await db.SaveChangesAsync();
        var service = new ConfigurationService(new TenantRepository(db), AuthorizationTests.Identity(), TimeProvider.System);
        var input = PersistenceTests.Input(); var metric = input.Criteria[0];
        input = input with { Criteria = [metric with { Weight = 0.25m }, metric with { CriterionId = "second", Weight = 0.75m }] };
        var original = await service.AddProfileVersionAsync("profile", input, 0);
        await service.AddProfileVersionAsync("profile", input with { Criteria = [metric with { Weight = 1m },
            metric with { CriterionId = "second", Weight = 0.75m, IsEnabled = false }] }, 1);
        var disabled = await service.ReadProfileAsync("profile", 2);
        Assert.Single(disabled.ActiveCriteria); Assert.Equal(2, disabled.Criteria.Length);
        await service.AddProfileVersionAsync("profile", input with { Criteria = [metric] }, 2);
        Assert.Single((await service.ReadProfileAsync("profile", 3)).Criteria);
        Assert.Equal(original.ContentHash, (await service.ReadProfileAsync("profile", 1)).ContentHash);
        Assert.Equal(0.25m, (await service.ReadProfileAsync("profile", 1)).Criteria[0].Weight);
        await Assert.ThrowsAsync<EvaluationValidationException>(() => service.AddProfileVersionAsync("invalid", input with
        { Criteria = [metric with { Weight = 0.99m }] }, 0));
        Assert.Equal(3, await db.Set<AuditEvent>().CountAsync());
    }

    [Fact]
    public async Task Activation_requires_validation_and_explicit_retirement_and_assignment_rejects_wrong_scope()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ConfigurationDbContext>().UseSqlite(connection).Options;
        await using var db = new ConfigurationDbContext(options, AuthorizationTests.Identity());
        await db.Database.EnsureCreatedAsync(); db.Add(PersistenceTests.Grant("a")); await db.SaveChangesAsync();
        var service = new ConfigurationService(new TenantRepository(db), AuthorizationTests.Identity(), TimeProvider.System);
        var input = PersistenceTests.Input(); await service.AddProfileVersionAsync("first", input, 0);
        await Assert.ThrowsAsync<OperationException>(() => service.TransitionAsync("first", 1, ProfileStatus.Active, 0, "skip"));
        await service.TransitionAsync("first", 1, ProfileStatus.Validated, 0, "validate");
        await service.TransitionAsync("first", 1, ProfileStatus.Active, 1, "activate");
        await service.AddProfileVersionAsync("second", input, 0);
        await service.TransitionAsync("second", 1, ProfileStatus.Validated, 0, "validate");
        await Assert.ThrowsAsync<OperationException>(() => service.TransitionAsync("second", 1, ProfileStatus.Active, 1, "conflict"));
        await Assert.ThrowsAsync<OperationException>(() => service.AssignAsync(new(ScopeType.Team, "team"), "first", 1, "wrong scope"));
        await service.TransitionAsync("first", 1, ProfileStatus.Archived, 2, "retire");
        await service.TransitionAsync("second", 1, ProfileStatus.Active, 1, "activate");
        await service.AssignAsync(input.Scope, "second", 1, "assign");
        Assert.Single(await db.Set<ProfileAssignment>().ToArrayAsync());
    }

    [Fact]
    public async Task Revoked_delegation_and_foreign_tenant_records_cannot_authorize_changes()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ConfigurationDbContext>().UseSqlite(connection).Options;
        await using var db = new ConfigurationDbContext(options, AuthorizationTests.Identity());
        await db.Database.EnsureCreatedAsync();
        var grant = PersistenceTests.Grant("a", scope: ScopeType.Team, scopeId: "team", role: AccessRole.EvaluationManager);
        db.Add(new TeamRegistration { TenantId = "a", Id = "team" });
        db.Add(grant); await db.SaveChangesAsync();
        var service = new ConfigurationService(new TenantRepository(db), AuthorizationTests.Identity(), TimeProvider.System);
        await service.AddProfileVersionAsync("profile", PersistenceTests.Input(scope: new(ScopeType.Team, "team")), 0);
        db.Remove(grant); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<OperationException>(() => service.TransitionAsync("profile", 1, ProfileStatus.Validated, 0, "denied"));
        await using var foreign = new ConfigurationDbContext(options, AuthorizationTests.Identity("b"));
        var foreignService = new ConfigurationService(new TenantRepository(foreign), AuthorizationTests.Identity("b"), TimeProvider.System);
        await Assert.ThrowsAsync<OperationException>(() => foreignService.ReadProfileAsync("profile", 1));
        Assert.Single(await db.Set<AuditEvent>().ToArrayAsync());
    }
}
