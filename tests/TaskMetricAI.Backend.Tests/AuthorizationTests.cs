using TaskMetricAI.Application;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Backend.Tests;

public sealed class AuthorizationTests
{
    internal static TenantIdentity Identity(string tenant = "a", string actor = "actor") =>
        new(tenant, "org", actor, "cloud", "install", "app", "env");

    [Theory]
    [InlineData(AccessRole.Developer, false)]
    [InlineData(AccessRole.Reviewer, false)]
    [InlineData(AccessRole.EvaluationManager, true)]
    [InlineData((AccessRole)99, false)]
    public async Task Only_explicit_manager_can_manage_exact_delegated_scope(AccessRole role, bool manage)
    {
        var store = new MemoryStore("a");
        store.Add(new TeamRegistration { TenantId = "a", Id = "team" });
        store.Add(new ScopeGrant { TenantId = "a", ActorId = "actor", Role = role,
            ScopeType = ScopeType.Team, ScopeId = "team" });
        var access = new AccessControl(store, Identity());
        if (manage) await access.RequireScopeAsync(new(ScopeType.Team, "team"), true);
        else await Assert.ThrowsAsync<OperationException>(() => access.RequireScopeAsync(new(ScopeType.Team, "team"), true));
        if (Enum.IsDefined(role)) await access.RequireScopeAsync(new(ScopeType.Team, "team"), false);
        else await Assert.ThrowsAsync<OperationException>(() => access.RequireScopeAsync(new(ScopeType.Team, "team"), false));
        await Assert.ThrowsAsync<OperationException>(() => access.RequireScopeAsync(new(ScopeType.Team, "other"), false));
        await Assert.ThrowsAsync<OperationException>(() => access.RequireAdministratorAsync());
    }

    [Fact]
    public async Task Administrator_is_tenant_and_organization_bound_and_does_not_imply_repository_access()
    {
        var store = new MemoryStore("a");
        store.Add(new ProjectRegistration { TenantId = "a", Id = "project" });
        var access = new AccessControl(store, Identity());
        await Assert.ThrowsAsync<OperationException>(() => access.RequireAdministratorAsync());
        store.Add(new ScopeGrant { TenantId = "a", ActorId = "actor", Role = AccessRole.SystemAdministrator,
            ScopeType = ScopeType.Organization, ScopeId = "org" });
        await access.RequireAdministratorAsync();
        await access.RequireScopeAsync(new(ScopeType.JiraProject, "project"), true);
        await Assert.ThrowsAsync<OperationException>(() => access.RequireScopeAsync(new(ScopeType.Organization, "other"), true));
        await Assert.ThrowsAsync<OperationException>(() => access.RequireRepositoryAsync("private"));
        Assert.Throws<OperationException>(() => new AccessControl(new MemoryStore("b"), Identity()));
        Assert.Throws<OperationException>(() => new TenantIdentity("", "org", "actor", "cloud", "install", "app", "env"));
    }

    [Fact]
    public async Task Authentication_requires_a_verified_exact_installation_binding()
    {
        var verifier = new Verifier();
        var directory = new Directory(null);
        var auth = new AuthenticateTenant(verifier, directory);
        await Assert.ThrowsAsync<OperationException>(() => auth.ExecuteAsync("invalid"));
        Assert.False(directory.Called);
        await Assert.ThrowsAsync<OperationException>(() => auth.ExecuteAsync("valid"));
        directory.Identity = Identity("a", "different-actor");
        await Assert.ThrowsAsync<OperationException>(() => auth.ExecuteAsync("valid"));
        directory.Identity = Identity();
        Assert.Equal("a", (await auth.ExecuteAsync("valid")).TenantId);
    }

    private sealed class Verifier : IForgeIdentityVerifier
    {
        public Task<VerifiedForgeIdentity> VerifyAsync(string token, CancellationToken cancellationToken = default) =>
            token == "valid" ? Task.FromResult(new VerifiedForgeIdentity("app", "env", "DEVELOPMENT", "install", "cloud", "actor"))
                : throw new OperationException(401, "invalid_forge_token");
    }
    private sealed class Directory(TenantIdentity? identity) : IInstallationDirectory
    {
        public TenantIdentity? Identity { get; set; } = identity;
        public bool Called { get; private set; }
        public Task<TenantIdentity?> ResolveAsync(VerifiedForgeIdentity identity, CancellationToken cancellationToken = default)
        { Called = true; return Task.FromResult(Identity); }
    }
}
