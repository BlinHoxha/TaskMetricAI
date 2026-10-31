using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;
using TaskMetricAI.Infrastructure.Authentication;
using TaskMetricAI.Infrastructure.Persistence;

namespace TaskMetricAI.Backend.Tests;

internal sealed class SecurityFixture : IAsyncDisposable
{
    public string Setup { get; }
    public string Runtime { get; }
    public string RuntimeFile { get; }
    public string AppId { get; } = "security-" + Guid.NewGuid().ToString("N");
    public string A { get; } = "a-" + Guid.NewGuid().ToString("N");
    public string B { get; } = "b-" + Guid.NewGuid().ToString("N");
    public string ProfileA { get; } = "profile-a-" + Guid.NewGuid().ToString("N");
    public string ProfileB { get; } = "profile-b-" + Guid.NewGuid().ToString("N");
    private readonly RSA rsa = RSA.Create(2048);
    public SecurityKey Key { get; }
    private SecurityFixture(string setup, string runtime, string runtimeFile)
    { Setup = setup; Runtime = runtime; RuntimeFile = runtimeFile; Key = new RsaSecurityKey(rsa) { KeyId = "fixture-key" }; }

    public static async Task<SecurityFixture> CreateAsync()
    {
        var setupPath = Environment.GetEnvironmentVariable("TASKMETRICAI_TEST_CONNECTION_FILE")
            ?? throw new InvalidOperationException("TASKMETRICAI_TEST_CONNECTION_FILE is required for PostgreSQL security tests.");
        var runtimePath = Environment.GetEnvironmentVariable("TASKMETRICAI_RUNTIME_TEST_CONNECTION_FILE")
            ?? throw new InvalidOperationException("TASKMETRICAI_RUNTIME_TEST_CONNECTION_FILE is required for PostgreSQL security tests.");
        var fixture = new SecurityFixture((await File.ReadAllTextAsync(setupPath)).Trim(), (await File.ReadAllTextAsync(runtimePath)).Trim(), runtimePath);
        if (new Npgsql.NpgsqlConnectionStringBuilder(fixture.Setup).Database != "taskmetricai_test")
            throw new InvalidOperationException("Only the isolated test database may be seeded.");
        foreach (var tenant in new[] { fixture.A, fixture.B })
        {
            await LocalDatabaseOperator.RegisterInstallationAsync(fixture.Setup, new TenantInstallation
            {
                TenantId = tenant, OrganizationId = "org", AppId = fixture.AppId, EnvironmentId = "env",
                InstallationId = tenant, CloudId = "11111111-1111-1111-1111-111111111111"
            }, "admin", "synthetic security fixture");
            await LocalDatabaseOperator.RegisterScopesAsync(fixture.Setup,
                new(tenant, "org", "admin", "11111111-1111-1111-1111-111111111111", tenant, fixture.AppId, "env"),
                ["project"], ["team"], [("project", "team")], "synthetic scope registration");
            await using var db = fixture.Operator(tenant);
            db.Add(PersistenceTests.Grant(tenant, actor: "developer", role: AccessRole.Developer));
            db.Add(PersistenceTests.Grant(tenant, actor: "manager", role: AccessRole.EvaluationManager, scope: TaskMetricAI.Evaluation.ScopeType.Team, scopeId: "team"));
            await db.SaveChangesAsync();
            await using var session = await fixture.Session(tenant);
            await fixture.Service(session).AddProfileVersionAsync(tenant == fixture.A ? fixture.ProfileA : fixture.ProfileB, PersistenceTests.Input(), 0);
            await session.CommitAsync();
        }
        return fixture;
    }

    public ConfigurationDbContext Operator(string tenant) => new(new DbContextOptionsBuilder<ConfigurationDbContext>().UseNpgsql(Setup).Options,
        new(tenant, "org", "admin", "11111111-1111-1111-1111-111111111111", tenant, AppId, "env"), operatorOnly: true);
    public string Token(string tenant, string actor = "admin", Action<JwtPayload>? alter = null)
    {
        var now = DateTime.UtcNow;
        var payload = new JwtPayload("forge/invocation-token", AppId, [], now.AddMinutes(-1), now.AddMinutes(5))
        {
            ["app"] = new Dictionary<string, object> { ["id"] = AppId, ["installationId"] = tenant,
                ["environment"] = new Dictionary<string, object> { ["id"] = "env", ["type"] = "DEVELOPMENT" } },
            ["context"] = new Dictionary<string, object> { ["cloudId"] = "11111111-1111-1111-1111-111111111111" }, ["principal"] = actor
        };
        alter?.Invoke(payload);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(new SigningCredentials(Key, SecurityAlgorithms.RsaSha256)), payload));
    }
    public async Task<AuthenticatedTenant> Bound(string tenant, string actor = "admin") =>
        await new AuthenticateTenant(new ForgeIdentityVerifier(new(AppId, ["DEVELOPMENT"]), new Keys(Key)),
            new RuntimeInstallationDirectory(Runtime)).AuthenticateAsync(Token(tenant, actor));
    public async Task<TenantDatabaseSession> Session(string tenant, string actor = "admin", string? connection = null) =>
        await TenantDatabaseSession.OpenAsync(connection ?? Runtime, await Bound(tenant, actor));
    public ConfigurationService Service(TenantDatabaseSession session) => new(new TenantRepository(session.Db), session.Identity, TimeProvider.System);
    public ValueTask DisposeAsync() { rsa.Dispose(); return ValueTask.CompletedTask; }
    internal sealed class Keys(SecurityKey key) : IForgeSigningKeys
    {
        public Task<IReadOnlyCollection<SecurityKey>> GetAsync(bool refresh, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<SecurityKey>>([key]);
    }
}
