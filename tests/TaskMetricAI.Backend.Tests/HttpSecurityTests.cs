using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TaskMetricAI.Application;
using TaskMetricAI.Evaluation;
using TaskMetricAI.Infrastructure.Authentication;

namespace TaskMetricAI.Backend.Tests;

[Trait("Category", "PostgreSQL")]
public sealed partial class HttpSecurityTests
{
    [Fact]
    public async Task Authentication_fails_closed_for_missing_invalid_forged_and_unregistered_tokens()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await using var factory = new ApiFactory(fixture); using var client = factory.CreateClient();
        var path = $"/api/profiles/{fixture.ProfileA}/versions/1";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        var corrupt = fixture.Token(fixture.A).Split('.'); corrupt[2] = "AAAA";
        var invalid = new[] { "malformed", string.Join('.', corrupt), fixture.Token("unregistered"),
            fixture.Token(fixture.A, alter: p => p["iss"] = "wrong"),
            fixture.Token(fixture.A, alter: p => p["aud"] = "wrong"),
            fixture.Token(fixture.A, alter: p => p["exp"] = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds()),
            fixture.Token(fixture.A, actor: ""),
            fixture.Token(fixture.A, alter: p => ((Dictionary<string, object>)p["app"])["id"] = "wrong"),
            fixture.Token(fixture.A, alter: p => ((Dictionary<string, object>)((Dictionary<string, object>)p["app"])["environment"])["type"] = "PRODUCTION"),
            fixture.Token(fixture.A, alter: p => ((Dictionary<string, object>)p["context"])["cloudId"] = "not-a-guid") };
        foreach (var token in invalid)
        { client.DefaultRequestHeaders.Authorization = new("Bearer", token); Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode); }
    }

    [Fact]
    public async Task Cross_tenant_reads_updates_activation_assignment_and_catalog_access_are_rejected()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await using (var b = await fixture.Session(fixture.B))
        { await fixture.Service(b).AddCatalogVersionAsync("catalog-" + fixture.B, [], 0, "synthetic catalog"); await b.CommitAsync(); }
        await using var factory = new ApiFactory(fixture); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", fixture.Token(fixture.A));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", fixture.B); client.DefaultRequestHeaders.Add("X-Actor-Id", "admin-b");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/profiles/{fixture.ProfileB}/versions/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/catalogs/catalog-{fixture.B}/versions/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/profiles/{fixture.ProfileB}/versions/1/lifecycle",
            new Lifecycle(ProfileStatus.Validated, 0, "foreign mutation"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/profile-assignments",
            new Assignment(new(ScopeType.Organization, "org"), fixture.ProfileB, 1, "foreign assignment"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/profiles/{fixture.ProfileB}/versions",
            new NewProfile(PersistenceTests.Input(), 1))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/catalogs/forged-selection/versions",
            new NewCatalog([new(fixture.B, "foreign", 1)], 0, "foreign metric reference"))).StatusCode);
        // The same client can still read its own resources; ignored headers do not change authority.
        var own = await client.GetFromJsonAsync<JsonElement>($"/api/profiles/{fixture.ProfileA}/versions/1");
        Assert.Equal(fixture.A, own.GetProperty("tenantId").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/profiles/{fixture.ProfileA}/versions",
            new { input = PersistenceTests.Input(), expectedVersion = 1, tenantId = fixture.B, actorId = "other", role = "SystemAdministrator" })).StatusCode);
        await using var verify = await fixture.Session(fixture.B);
        Assert.Equal(ProfileStatus.Draft, (await fixture.Service(verify).ReadProfileAsync(fixture.ProfileB, 1)).Status);
    }

    [Fact]
    public async Task Client_role_claims_cannot_escalate_developer_or_manager_privileges_and_bootstrap_is_not_exposed()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await using var factory = new ApiFactory(fixture); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", fixture.Token(fixture.A, "developer", p =>
        { p["roles"] = new[] { "SystemAdministrator" }; p["tenantId"] = fixture.B; }));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/scope-grants",
            new Grant("developer", TaskMetricAI.Domain.AccessRole.SystemAdministrator, new(ScopeType.Organization, "org"), "escalate"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/metrics/forged/versions",
            new NewMetric(new("m", "d", new("1", "2", "3", "4", "5"), "unauthorized"), 0))).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", fixture.Token(fixture.A, "manager"));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/profiles/team-only/versions",
            new NewProfile(PersistenceTests.Input(scope: new(ScopeType.Team, "team")), 0))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/profiles/other-team/versions",
            new NewProfile(PersistenceTests.Input(scope: new(ScopeType.Team, "other")), 0))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/bootstrap", new { tenantId = fixture.B })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", fixture.Token(fixture.A, "ungranted-actor"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/profiles/{fixture.ProfileA}/versions/1")).StatusCode);
    }

    [Fact]
    public async Task Administrator_operations_succeed_and_record_scoped_audit_without_changing_history()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await using var factory = new ApiFactory(fixture); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", fixture.Token(fixture.A));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/scope-grants",
            new Grant("new-manager", TaskMetricAI.Domain.AccessRole.EvaluationManager, new(ScopeType.Team, "team"), "delegate configuration"))).StatusCode);
        var metric = await client.PostAsJsonAsync("/api/metrics/http-custom/versions", new NewMetric(new("Custom", "Description", new("1","2","3","4","5"), "create"), 0));
        Assert.Equal(HttpStatusCode.OK, metric.StatusCode);
        await using var session = await fixture.Session(fixture.A);
        var audit = (await new TaskMetricAI.Infrastructure.Persistence.TenantRepository(session.Db)
            .FindAsync<TaskMetricAI.Domain.AuditEvent>(x => x.Operation == "scope.granted")).Single();
        Assert.Equal("admin", audit.ActorId); Assert.Equal("new-manager", audit.TargetActorId);
        Assert.Equal(ScopeType.Team, audit.ScopeType); Assert.Equal("team", audit.ScopeId);
        using var state = JsonDocument.Parse(audit.AfterStateJson!);
        Assert.Equal((int)TaskMetricAI.Domain.AccessRole.EvaluationManager, state.RootElement.GetProperty("role").GetInt32());
    }

    private sealed class ApiFactory(SecurityFixture fixture, HttpMessageHandler? jira = null,
        Microsoft.Extensions.Logging.ILoggerProvider? logs = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            if (logs is not null) builder.ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Trace);
                logging.AddProvider(logs);
            });
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TASKMETRICAI_RUNTIME_CONNECTION_FILE"] = fixture.RuntimeFile,
                ["TASKMETRICAI_FORGE_APP_ID"] = fixture.AppId,
                ["TASKMETRICAI_FORGE_ENVIRONMENTS"] = "DEVELOPMENT"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IForgeSigningKeys>(); services.AddSingleton<IForgeSigningKeys>(new SecurityFixture.Keys(fixture.Key));
                if (jira is not null) services.AddHttpClient("jira").ConfigurePrimaryHttpMessageHandler(() => jira);
            });
        }
    }
}
