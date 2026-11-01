using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;
using TaskMetricAI.Infrastructure.Persistence;

namespace TaskMetricAI.Backend.Tests;

public sealed partial class HttpSecurityTests
{
    private const string JiraPath = "/api/projects/100/jira/issues/200";

    [Fact]
    public async Task Jira_read_uses_real_FIT_binding_RLS_and_leaves_all_tenant_rows_unchanged_without_disclosure()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await RegisterJira(fixture, fixture.A); await RegisterJira(fixture, fixture.B);
        var beforeA = await Snapshot(fixture, fixture.A); var beforeB = await Snapshot(fixture, fixture.B);
        using var logs = new JiraLogs(); using var handler = new JiraHandler();
        await using var factory = new ApiFactory(fixture, handler, logs); using var client = factory.CreateClient();
        using var request = JiraRequestMessage(fixture, fixture.A);
        request.Headers.Add("X-Tenant-Id", fixture.B); request.Headers.Add("X-Actor-Id", "admin");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "componentIds", "descriptionText", "id", "key", "projectId", "summary" },
            json.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal("private-description", json.GetProperty("descriptionText").GetString());
        Assert.Equal(beforeA, await Snapshot(fixture, fixture.A)); Assert.Equal(beforeB, await Snapshot(fixture, fixture.B));
        AssertNoJiraDisclosure(logs, request.Headers.Authorization!.Parameter!);
        Assert.Equal(3, handler.Paths.Count);
    }

    [Theory]
    [InlineData("anonymous", 401)] [InlineData("invalid-fit", 401)] [InlineData("unknown-install", 401)]
    [InlineData("no-grant", 404)] [InlineData("unregistered-project", 404)] [InlineData("foreign-grant", 404)]
    [InlineData("missing-base", 404)] [InlineData("bad-base", 404)] [InlineData("missing-token", 401)]
    [InlineData("duplicate-token", 401)] [InlineData("system-token", 401)] [InlineData("query", 422)]
    [InlineData("invalid-id", 422)]
    public async Task Jira_boundary_denies_before_any_provider_request(string scenario, int status)
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        if (scenario != "unregistered-project") await RegisterJira(fixture, fixture.A);
        await RegisterJira(fixture, fixture.B);
        using var handler = new JiraHandler(); await using var factory = new ApiFactory(fixture, handler);
        using var client = factory.CreateClient();
        var actor = scenario is "no-grant" or "foreign-grant" ? "ung ranted" : "actor";
        if (scenario == "foreign-grant")
        {
            await using var session = await fixture.Session(fixture.B);
            await fixture.Service(session).GrantAsync(actor, AccessRole.Developer, new(ScopeType.JiraProject, "100"), "foreign grant");
            await session.CommitAsync();
        }
        using var request = JiraRequestMessage(fixture, scenario == "unknown-install" ? "unregistered" : fixture.A, actor,
            scenario == "query" ? JiraPath + "?fields=*all&tenantId=" + fixture.B :
            scenario == "invalid-id" ? "/api/projects/100/jira/issues/TASK-1" : JiraPath,
            scenario == "missing-base" ? null : scenario == "bad-base" ? "https://evil.invalid/private-secret" : JiraProviderTests.Base);
        if (scenario == "anonymous") request.Headers.Authorization = null;
        if (scenario == "invalid-fit") request.Headers.Authorization = new("Bearer", "invalid-fit");
        if (scenario is "missing-token" or "system-token") request.Headers.Remove("x-forge-oauth-user");
        if (scenario == "duplicate-token") request.Headers.Add("x-forge-oauth-user", "second-secret");
        request.Headers.Add("x-forge-oauth-system", "system-secret");
        request.Headers.Add("X-Tenant-Id", fixture.B); request.Headers.Add("X-Actor-Id", "admin");
        var response = await client.SendAsync(request);
        Assert.Equal(status, (int)response.StatusCode); Assert.Empty(handler.Paths);
        Assert.DoesNotContain("secret", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Jira_revoked_grant_denies_same_FIT_and_does_not_call_provider_again()
    {
        await using var fixture = await SecurityFixture.CreateAsync(); await RegisterJira(fixture, fixture.A);
        using var handler = new JiraHandler(); await using var factory = new ApiFactory(fixture, handler); using var client = factory.CreateClient();
        var token = JiraToken(fixture, fixture.A);
        using (var request = JiraRequestMessage(fixture, fixture.A))
        { request.Headers.Authorization = new("Bearer", token); Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode); }
        await using (var session = await fixture.Session(fixture.A))
        {
            var grant = (await new TenantRepository(session.Db).FindAsync<ScopeGrant>(g => g.ActorId == "actor")).Single();
            await fixture.Service(session).RevokeGrantAsync(grant.Id, "revoke jira access"); await session.CommitAsync();
        }
        using var denied = JiraRequestMessage(fixture, fixture.A); denied.Headers.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(denied)).StatusCode); Assert.Equal(3, handler.Paths.Count);
    }

    [Theory]
    [InlineData("actor", 404)] [InlineData("permission", 404)] [InlineData("project", 404)]
    [InlineData("body", 502)] [InlineData("transport", 502)] [InlineData("rate", 429)]
    public async Task Jira_provider_denials_return_safe_errors_and_logs(string scenario, int status)
    {
        await using var fixture = await SecurityFixture.CreateAsync(); await RegisterJira(fixture, fixture.A);
        using var logs = new JiraLogs();
        using var handler = new JiraHandler((request, _) =>
        {
            if (scenario == "transport") throw new HttpRequestException("private-provider-body opaque-secret");
            if (scenario is "permission" or "body" or "rate")
            {
                var response = JiraProviderTests.Json("private-provider-body opaque-secret", scenario == "permission" ? HttpStatusCode.Forbidden :
                    scenario == "rate" ? HttpStatusCode.TooManyRequests : HttpStatusCode.OK);
                response.Headers.RetryAfter = new(TimeSpan.FromSeconds(45)); return Task.FromResult(response);
            }
            return Task.FromResult(JiraProviderTests.Json(request.RequestUri!.AbsolutePath.EndsWith("myself", StringComparison.Ordinal)
                ? scenario == "actor" ? """{"accountId":"other"}""" : """{"accountId":"actor"}"""
                : JiraProviderTests.Metadata.Replace("\"100\"", "\"999\"", StringComparison.Ordinal)));
        });
        await using var factory = new ApiFactory(fixture, handler, logs); using var client = factory.CreateClient();
        using var request = JiraRequestMessage(fixture, fixture.A); var response = await client.SendAsync(request);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.DoesNotContain("private", await response.Content.ReadAsStringAsync());
        AssertNoJiraDisclosure(logs, request.Headers.Authorization!.Parameter!);
        if (scenario == "rate") Assert.Equal(TimeSpan.FromSeconds(45), response.Headers.RetryAfter!.Delta);
    }

    [Fact]
    public async Task Jira_concurrent_tenants_with_identical_ids_keep_actor_credentials_isolated()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await RegisterJira(fixture, fixture.A); await RegisterJira(fixture, fixture.B, "actor-b");
        using var handler = new JiraHandler(async (request, ct) =>
        {
            await Task.Yield(); ct.ThrowIfCancellationRequested();
            var credential = request.Headers.Authorization!.Parameter!;
            var actor = credential == "token-a" ? "actor" : credential == "token-b" ? "actor-b" : throw new InvalidOperationException("invalid test credential");
            return request.RequestUri!.AbsolutePath.EndsWith("myself", StringComparison.Ordinal)
                ? JiraProviderTests.Json(JsonSerializer.Serialize(new { accountId = actor })) : JiraHandler.Default(request);
        });
        await using var factory = new ApiFactory(fixture, handler); using var client = factory.CreateClient();
        await Task.WhenAll(Enumerable.Range(0, 12).Select(async i =>
        {
            using var request = JiraRequestMessage(fixture, i % 2 == 0 ? fixture.A : fixture.B, i % 2 == 0 ? "actor" : "actor-b");
            request.Headers.Remove("x-forge-oauth-user"); request.Headers.Add("x-forge-oauth-user", i % 2 == 0 ? "token-a" : "token-b");
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);
        }));
        Assert.Equal(36, handler.Paths.Count);
    }

    private static async Task RegisterJira(SecurityFixture fixture, string tenant, string actor = "actor")
    {
        await LocalDatabaseOperator.RegisterScopesAsync(fixture.Setup,
            new(tenant, "org", "admin", JiraProviderTests.Cloud, tenant, fixture.AppId, "env"), ["100"], [], [], "synthetic Jira scope");
        await using var session = await fixture.Session(tenant);
        await fixture.Service(session).GrantAsync(actor, AccessRole.Developer, new(ScopeType.JiraProject, "100"), "synthetic Jira read");
        await session.CommitAsync();
    }

    private static string JiraToken(SecurityFixture fixture, string tenant, string actor = "actor", string? apiBase = JiraProviderTests.Base) =>
        fixture.Token(tenant, actor, payload =>
        {
            if (apiBase is not null) ((Dictionary<string, object>)payload["app"])["apiBaseUrl"] = apiBase;
        });

    private static HttpRequestMessage JiraRequestMessage(SecurityFixture fixture, string tenant, string actor = "actor",
        string path = JiraPath, string? apiBase = JiraProviderTests.Base)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new("Bearer", JiraToken(fixture, tenant, actor, apiBase));
        request.Headers.Add("x-forge-oauth-user", "opaque-secret"); return request;
    }

    private static async Task<string[]> Snapshot(SecurityFixture fixture, string tenant)
    {
        await using var db = fixture.Operator(tenant); var rows = new List<string>();
        foreach (var table in db.Model.GetEntityTypes().Select(e => e.GetTableName()!).Distinct().Order())
        {
            // Identifier is fixed EF model metadata; the tenant remains a bound SQL parameter.
            var sql = $"SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text),'[]'::jsonb)::text AS \"Value\" FROM public.\"{table}\" t WHERE \"TenantId\" = {{0}}";
            rows.Add(await db.Database.SqlQueryRaw<string>(sql, tenant).SingleAsync());
        }
        return rows.ToArray();
    }

    private static void AssertNoJiraDisclosure(JiraLogs logs, string fit)
    {
        var output = string.Join('\n', logs.Entries);
        foreach (var secret in new[] { fit, "opaque-secret", "private-summary", "private-description", "private-provider-body",
            "private-comment", "private-custom", "private-email", "x-forge-oauth-user", "Authorization:" })
            Assert.DoesNotContain(secret, output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(logs.Entries, e => e.Contains("System.Net.Http.HttpClient.jira", StringComparison.Ordinal));
    }

    private sealed class JiraLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Capture(categoryName, Entries);
        public void Dispose() { }
        private sealed class Capture(string category, ConcurrentQueue<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                entries.Enqueue(category + " " + formatter(state, exception) + " " + exception);
        }
    }
}
