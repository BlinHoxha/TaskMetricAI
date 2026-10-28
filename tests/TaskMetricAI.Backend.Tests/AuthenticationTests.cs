using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;
using TaskMetricAI.Infrastructure.Authentication;

namespace TaskMetricAI.Backend.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task Forge_verifier_requires_signed_app_environment_installation_cloud_and_actor()
    {
        using var rsa = RSA.Create(2048);
        using var other = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "trusted" };
        var verifier = new ForgeIdentityVerifier(new("app-id", ["DEVELOPMENT"]), new Keys(key));
        var identity = await verifier.VerifyAsync(Token(key));
        Assert.Equal("actor", identity.ActorId);
        Assert.Equal("installation", identity.InstallationId);
        foreach (var token in new[]
        {
            Token(new RsaSecurityKey(other) { KeyId = "other" }), Token(key, audience: "wrong"),
            Token(key, issuer: "wrong"), Token(key, appId: "wrong"), Token(key, env: "PRODUCTION"),
            Token(key, actor: ""), Token(key, cloud: "not-a-guid"), Token(key, expired: true)
        })
            await Assert.ThrowsAsync<OperationException>(() => verifier.VerifyAsync(token));
    }

    [Fact]
    public async Task Delegated_manager_cannot_manage_other_scopes_or_integrations()
    {
        var identity = new TenantIdentity("tenant-a", "org", "manager", "cloud", "install", "app", "env");
        var store = new MemoryStore(identity.TenantId);
        store.Add(new ScopeGrant { TenantId = "tenant-a", ActorId = "manager", Role = AccessRole.EvaluationManager,
            ScopeType = ScopeType.Team, ScopeId = "team-a" });
        store.Add(new ScopeGrant { TenantId = "tenant-b", ActorId = "manager", Role = AccessRole.SystemAdministrator,
            ScopeType = ScopeType.Organization, ScopeId = "org" });
        var access = new AccessControl(store, identity);
        await access.RequireScopeAsync(new(ScopeType.Team, "team-a"), true);
        await Assert.ThrowsAsync<OperationException>(() => access.RequireScopeAsync(new(ScopeType.Team, "team-b"), true));
        await Assert.ThrowsAsync<OperationException>(() => access.RequireAdministratorAsync());
        await Assert.ThrowsAsync<OperationException>(() => access.RequireRepositoryAsync("repo"));
    }

    internal static string Token(SecurityKey key, string audience = "app-id", string issuer = "forge/invocation-token",
        string appId = "app-id", string env = "DEVELOPMENT", string actor = "actor", string? cloud = null, bool expired = false)
    {
        var now = DateTime.UtcNow;
        var payload = new JwtPayload(issuer, audience, [], now.AddMinutes(-10), expired ? now.AddMinutes(-5) : now.AddMinutes(5))
        {
            ["app"] = new { id = appId, installationId = "installation", environment = new { id = "environment", type = env } },
            ["context"] = new { cloudId = cloud ?? "11111111-1111-1111-1111-111111111111" }, ["principal"] = actor
        };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(new SigningCredentials(key, SecurityAlgorithms.RsaSha256)), payload));
    }

    private sealed class Keys(SecurityKey key) : IForgeSigningKeys
    {
        public Task<IReadOnlyCollection<SecurityKey>> GetAsync(bool refresh, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<SecurityKey>>([key]);
    }
}

internal sealed class MemoryStore(string tenant) : ITenantRepository
{
    private readonly List<TenantEntity> entities = [];
    public Task<IReadOnlyList<T>> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : TenantEntity =>
        Task.FromResult<IReadOnlyList<T>>(entities.OfType<T>().Where(e => e.TenantId == tenant).Where(predicate.Compile()).ToArray());
    public void Add<T>(T entity) where T : TenantEntity => entities.Add(entity);
    public void Remove<T>(T entity) where T : TenantEntity => entities.Remove(entity);
    public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
