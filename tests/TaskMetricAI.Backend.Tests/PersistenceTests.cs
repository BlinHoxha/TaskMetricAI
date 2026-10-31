using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;
using TaskMetricAI.Infrastructure.Persistence;

namespace TaskMetricAI.Backend.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task Tenant_filters_apply_to_same_ids_and_cross_tenant_writes_fail()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ConfigurationDbContext>().UseSqlite(connection).Options;
        await using var a = new ConfigurationDbContext(options, AuthorizationTests.Identity());
        await a.Database.EnsureCreatedAsync();
        a.Add(Grant("a", "shared")); await a.SaveChangesAsync();
        await using var b = new ConfigurationDbContext(options, AuthorizationTests.Identity("b"));
        b.Add(Grant("b", "shared")); await b.SaveChangesAsync();
        Assert.Equal("a", (await a.Set<ScopeGrant>().SingleAsync()).TenantId);
        Assert.Equal("b", (await b.Set<ScopeGrant>().SingleAsync()).TenantId);
        a.Add(Grant("b", "foreign"));
        await Assert.ThrowsAsync<OperationException>(() => a.SaveChangesAsync());
        a.ChangeTracker.Clear();
        a.Attach(Grant("b", "shared")); a.Remove(a.Set<ScopeGrant>().Local.Single());
        await Assert.ThrowsAsync<OperationException>(() => a.SaveChangesAsync());
    }

    [Fact]
    public async Task Composite_foreign_key_rejects_other_tenants_profile_even_with_filters_bypassed()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ConfigurationDbContext>().UseSqlite(connection).Options;
        await using var b = new ConfigurationDbContext(options, AuthorizationTests.Identity("b"));
        await b.Database.EnsureCreatedAsync();
        var row = ProfileRow("b"); b.Add(row); await b.SaveChangesAsync();
        await using var a = new ConfigurationDbContext(options, AuthorizationTests.Identity());
        a.Add(new ProfileAssignment { TenantId = "a", ScopeId = "org", ScopeType = ScopeType.Organization, ProfileVersionId = row.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => a.SaveChangesAsync());
    }

    [Fact]
    public async Task History_content_and_audit_are_immutable_and_versions_are_unique()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ConfigurationDbContext>().UseSqlite(connection).Options;
        await using var db = new ConfigurationDbContext(options, AuthorizationTests.Identity());
        await db.Database.EnsureCreatedAsync();
        var row = ProfileRow("a"); db.Add(row); await db.SaveChangesAsync();
        row.SnapshotJson = "{}";
        await Assert.ThrowsAsync<OperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        db.Add(ProfileRow("a")); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var audit = new AuditEvent { TenantId = "a", ActorId = "actor", Reason = "test", Operation = "test", SubjectId = "profile" };
        db.Add(audit); await db.SaveChangesAsync(); audit.Reason = "rewritten";
        await Assert.ThrowsAsync<OperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Lifecycle_uses_optimistic_concurrency_and_rejects_skipped_states()
    {
        using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ConfigurationDbContext>().UseSqlite(connection).Options;
        await using var first = new ConfigurationDbContext(options, AuthorizationTests.Identity());
        await first.Database.EnsureCreatedAsync(); var row = ProfileRow("a"); first.Add(row); await first.SaveChangesAsync();
        await using var second = new ConfigurationDbContext(options, AuthorizationTests.Identity());
        var stale = await second.Set<ProfileVersion>().SingleAsync();
        row.Status = ProfileStatus.Validated; await first.SaveChangesAsync();
        stale.Status = ProfileStatus.Validated; await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        second.ChangeTracker.Clear();
        var current = await second.Set<ProfileVersion>().SingleAsync(); current.Status = ProfileStatus.Archived;
        await Assert.ThrowsAsync<OperationException>(() => second.SaveChangesAsync());
    }

    internal static ScopeGrant Grant(string tenant, string? id = null, string actor = "actor", AccessRole role = AccessRole.SystemAdministrator,
        ScopeType scope = ScopeType.Organization, string scopeId = "org") => new()
    { TenantId = tenant, Id = id ?? Guid.NewGuid().ToString("N"), ActorId = actor, Role = role, ScopeType = scope, ScopeId = scopeId };

    internal static ProfileInput Input(string description = "profile", ProfileScope? scope = null) => new("custom", description,
        scope ?? new(ScopeType.Organization, "org"), [new("custom-only", "Custom", "Description", 1m, new("one", "two", "three", "four", "five"))],
        [new("all", "All", new(1, 5, true, true))], null, new(), "test change");

    internal static ProfileVersion ProfileRow(string tenant)
    {
        var input = Input();
        var profile = new EvaluationProfile(tenant, "profile", 1, input.Name, input.Description, input.Scope,
            input.Criteria, input.ComplexityBands, new("actor", DateTimeOffset.UtcNow, "test"));
        return new() { TenantId = tenant, ProfileId = profile.ProfileId, Version = profile.Version, ScopeType = profile.Scope.Type,
            ScopeId = profile.Scope.Id, SnapshotJson = Documents.Write(ProfileDocument.From(profile)), ContentHash = profile.ContentHash };
    }
}
