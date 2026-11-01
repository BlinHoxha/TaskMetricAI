using System.Net;
using System.Text;
using System.Text.Json;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;
using TaskMetricAI.Infrastructure;

namespace TaskMetricAI.Backend.Tests;

public sealed class JiraProviderTests
{
    internal const string Cloud = "11111111-1111-1111-1111-111111111111";
    internal const string Base = "https://api.atlassian.com/ex/jira/" + Cloud;
    internal const string Metadata = """{"id":"200","fields":{"project":{"id":"100"}}}""";
    internal const string Full = """{"id":"200","key":"TASK-1","self":"https://do-not-follow.invalid","fields":{"project":{"id":"100","name":"private-project"},"summary":"private-summary","description":{"type":"doc","version":1,"content":[{"type":"paragraph","content":[{"type":"text","text":"private-description","marks":[{"type":"link","attrs":{"href":"https://do-not-follow.invalid"}}]},{"type":"mention","attrs":{"text":"private-person"}}]}]},"components":[{"id":"300","name":"private-component"}],"comment":{"comments":[{"body":"private-comment"}]},"customfield_1":"private-custom","reporter":{"emailAddress":"private-email"}}}""";

    internal static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    internal static Task<AuthenticatedTenant> Bound(string? apiBase = Base, string tenant = "tenant", string actor = "actor") =>
        new AuthenticateTenant(new Verifier(new("app", "env", "DEVELOPMENT", tenant, Cloud, actor, apiBase)),
            new Directory(new(tenant, "org", actor, Cloud, tenant, "app", "env"))).AuthenticateAsync("synthetic-fit");

    [Fact]
    public async Task Uses_only_fixed_GETs_and_allowlisted_projection()
    {
        using var handler = new JiraHandler(); using var http = new HttpClient(handler);
        var result = await new JiraCloudReader(http).ReadIssueAsync(await Bound(), new("opaque-secret"), "100", "200");
        Assert.Equal("private-description", result.DescriptionText);
        Assert.Equal(new[] { "300" }, result.ComponentIds);
        Assert.Equal(new[] { "/ex/jira/" + Cloud + "/rest/api/3/myself",
            "/ex/jira/" + Cloud + "/rest/api/3/issue/200?fields=project&updateHistory=false",
            "/ex/jira/" + Cloud + "/rest/api/3/issue/200?fields=project,summary,description,components&updateHistory=false" }, handler.Paths.ToArray());
        var json = JsonSerializer.Serialize(result);
        foreach (var secret in new[] { "private-comment", "private-custom", "private-email", "private-person", "private-project", "private-component", "do-not-follow" })
            Assert.DoesNotContain(secret, json);
        Assert.Equal("JiraIssue [redacted]", result.ToString());
        Assert.Null(http.DefaultRequestHeaders.Authorization);
    }

    [Theory]
    [InlineData(null)] [InlineData("http://api.atlassian.com/ex/jira/11111111-1111-1111-1111-111111111111")]
    [InlineData("https://api.atlassian.com.evil/ex/jira/11111111-1111-1111-1111-111111111111")]
    [InlineData("https://api.atlassian.com@evil/ex/jira/11111111-1111-1111-1111-111111111111")]
    [InlineData("https://api.atlassian.com:444/ex/jira/11111111-1111-1111-1111-111111111111")]
    [InlineData("https://api.atlassian.com/ex/jira/22222222-2222-2222-2222-222222222222")]
    [InlineData("https://api.atlassian.com/ex/confluence/11111111-1111-1111-1111-111111111111")]
    [InlineData(Base + "/")] [InlineData(Base + "?secret=x")] [InlineData(Base + "#x")]
    [InlineData(Base + "/../other")] [InlineData("https://127.0.0.1/")]
    public async Task Untrusted_bases_never_receive_credentials(string? apiBase)
    {
        using var handler = new JiraHandler(); using var http = new HttpClient(handler);
        var tenant = await Bound(apiBase);
        var ex = await Assert.ThrowsAsync<OperationException>(() => new JiraCloudReader(http)
            .ReadIssueAsync(tenant, new("secret"), "100", "200"));
        Assert.Equal(403, ex.Status); Assert.Empty(handler.Paths);
    }

    [Theory]
    [InlineData(401, 403)] [InlineData(403, 403)] [InlineData(404, 403)] [InlineData(429, 429)]
    [InlineData(302, 502)] [InlineData(500, 502)] [InlineData(204, 502)]
    public async Task Provider_failures_do_not_disclose_body_or_follow_redirects(int status, int expected)
    {
        using var handler = new JiraHandler((_, _) =>
        {
            var response = Json("provider-body-secret", (HttpStatusCode)status);
            response.Headers.Location = new("https://evil.invalid/secret");
            response.Headers.RetryAfter = new(TimeSpan.FromSeconds(30));
            return Task.FromResult(response);
        });
        using var http = new HttpClient(handler);
        var ex = await Assert.ThrowsAsync<OperationException>(() => Read(http));
        Assert.Equal(expected, ex.Status); Assert.DoesNotContain("secret", ex.ToString());
        Assert.Null(ex.InnerException); Assert.Single(handler.Paths);
        if (status == 429) Assert.Equal(30, ex.RetryAfterSeconds);
    }

    [Theory]
    [InlineData("actor")] [InlineData("project")] [InlineData("moved")] [InlineData("issue")]
    public async Task Actor_project_and_issue_substitution_fail_closed(string scenario)
    {
        using var handler = new JiraHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("myself", StringComparison.Ordinal))
                return Task.FromResult(Json(scenario == "actor" ? """{"accountId":"other"}""" : """{"accountId":"actor"}"""));
            var full = request.RequestUri.Query.Contains("summary", StringComparison.Ordinal);
            var json = full ? Full : Metadata;
            if (scenario == "project" || (scenario == "moved" && full)) json = json.Replace("\"100\"", "\"999\"", StringComparison.Ordinal);
            if (scenario == "issue") json = json.Replace("\"200\"", "\"999\"", StringComparison.Ordinal);
            return Task.FromResult(Json(json));
        });
        using var http = new HttpClient(handler);
        Assert.Equal(403, (await Assert.ThrowsAsync<OperationException>(() => Read(http))).Status);
        Assert.Equal(scenario == "actor" ? 1 : scenario == "moved" ? 3 : 2, handler.Paths.Count);
    }

    [Theory]
    [InlineData("malformed")] [InlineData("duplicate")] [InlineData("type")] [InlineData("large")]
    [InlineData("stream")] [InlineData("depth")] [InlineData("description")] [InlineData("shape")]
    public async Task Invalid_or_excessive_content_has_safe_errors(string scenario)
    {
        using var handler = new JiraHandler((request, _) =>
        {
            if (!request.RequestUri!.Query.Contains("summary", StringComparison.Ordinal)) return Task.FromResult(JiraHandler.Default(request));
            var body = scenario switch
            {
                "malformed" => "private-body{",
                "duplicate" => Full.Replace("\"id\":\"200\"", "\"id\":\"999\",\"id\":\"200\"", StringComparison.Ordinal),
                "large" or "stream" => new string('x', 1_048_577),
                "depth" => new string('[', 40) + "0" + new string(']', 40),
                "description" => Full.Replace("private-description", new string('x', 32769), StringComparison.Ordinal),
                "shape" => Full.Replace("\"components\":[", "\"components\":null,\"ignored\":[", StringComparison.Ordinal),
                _ => Full
            };
            var response = Json(body);
            if (scenario == "type") response.Content.Headers.ContentType = new("text/html");
            if (scenario == "stream") response.Content = new UnknownLengthContent(body);
            if (scenario == "stream") response.Content.Headers.ContentType = new("application/json");
            return Task.FromResult(response);
        });
        using var http = new HttpClient(handler);
        var ex = await Assert.ThrowsAsync<OperationException>(() => Read(http));
        Assert.Equal(502, ex.Status); Assert.Null(ex.InnerException); Assert.DoesNotContain("private", ex.ToString());
    }

    [Fact]
    public async Task Transport_exceptions_and_cancellation_never_retain_private_messages()
    {
        using var handler = new JiraHandler((_, _) => throw new HttpRequestException("opaque-secret private-body"));
        using var http = new HttpClient(handler);
        var ex = await Assert.ThrowsAsync<OperationException>(() => Read(http));
        Assert.DoesNotContain("secret", ex.ToString()); Assert.Null(ex.InnerException);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var cancellation = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Read(http, cancelled.Token));
        Assert.Null(cancellation.InnerException);
    }

    [Fact]
    public async Task Shared_client_keeps_concurrent_credentials_on_their_own_requests()
    {
        using var handler = new JiraHandler(async (request, ct) =>
        {
            await Task.Yield(); ct.ThrowIfCancellationRequested();
            var token = request.Headers.Authorization!.Parameter!;
            Assert.Contains(token, new[] { "token-a", "token-b" });
            if (request.RequestUri!.AbsolutePath.EndsWith("myself", StringComparison.Ordinal))
                return Json(JsonSerializer.Serialize(new { accountId = token == "token-a" ? "actor-a" : "actor-b" }));
            return JiraHandler.Default(request);
        });
        using var http = new HttpClient(handler); var reader = new JiraCloudReader(http);
        var a = await Bound(tenant: "a", actor: "actor-a"); var b = await Bound(tenant: "b", actor: "actor-b");
        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => reader.ReadIssueAsync(i % 2 == 0 ? a : b,
            new(i % 2 == 0 ? "token-a" : "token-b"), "100", "200")));
        Assert.Equal(60, handler.Paths.Count); Assert.Null(http.DefaultRequestHeaders.Authorization);
    }

    [Theory]
    [InlineData("")] [InlineData("0")] [InlineData("01")] [InlineData("TASK-1")]
    [InlineData("1/../2")] [InlineData("1?fields=*all")] [InlineData("１２")]
    public void Noncanonical_ids_are_rejected(string value) => Assert.Throws<OperationException>(() => JiraRequest.RequireId(value));

    [Theory]
    [InlineData("")] [InlineData(" ")] [InlineData("one,two")] [InlineData("one two")]
    [InlineData("one\r\ntwo")]
    public void Invalid_credentials_have_redacted_errors(string value)
    { Assert.Equal("jira_user_token_required", Assert.Throws<OperationException>(() => new JiraUserCredential(value)).Message); }

    private static async Task<JiraIssue> Read(HttpClient http, CancellationToken ct = default) =>
        await new JiraCloudReader(http).ReadIssueAsync(await Bound(), new("opaque-secret"), "100", "200", ct);

    [Fact]
    public async Task Actual_reader_deadline_cancels_an_unresponsive_provider()
    {
        using var handler = new JiraHandler(async (_, ct) =>
        { await Task.Delay(Timeout.InfiniteTimeSpan, ct); return Json("unreachable-private-content"); });
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var pending = Read(http);
        var ex = await Assert.ThrowsAsync<OperationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(22)));
        Assert.Equal(504, ex.Status); Assert.Equal("jira_timeout", ex.Message); Assert.Single(handler.Paths);
    }

    [Theory]
    [InlineData("null")] [InlineData("unknown-node")] [InlineData("empty")]
    public async Task Description_projection_is_nullable_and_never_copies_unsupported_attributes(string scenario)
    {
        using var handler = new JiraHandler((request, _) =>
        {
            if (!request.RequestUri!.Query.Contains("summary", StringComparison.Ordinal)) return Task.FromResult(JiraHandler.Default(request));
            var adf = scenario == "null" ? "null" : scenario == "empty" ? """{"type":"doc","version":1,"content":[]}""" :
                """{"type":"doc","version":1,"content":[{"type":"inlineCard","attrs":{"url":"https://private-url.invalid"},"content":[{"type":"text","text":"private-attribute"}]}]}""";
            var body = JsonSerializer.Serialize(new { id = "200", key = "TASK-1", fields = new
            { project = new { id = "100" }, summary = "summary", description = JsonSerializer.Deserialize<JsonElement>(adf), components = Array.Empty<object>() } });
            return Task.FromResult(Json(body));
        });
        using var http = new HttpClient(handler);
        var result = await Read(http); Assert.Equal(scenario == "null" ? null : "", result.DescriptionText);
    }

    [Fact]
    public async Task Transport_configuration_disables_redirects_cookies_and_sensitive_diagnostic_payloads()
    {
        using var transport = JiraCloudReader.CreateTransport();
        var sockets = Assert.IsType<SocketsHttpHandler>(Assert.IsAssignableFrom<DelegatingHandler>(transport).InnerHandler);
        Assert.False(sockets.AllowAutoRedirect); Assert.False(sockets.UseCookies); Assert.Null(sockets.ActivityHeadersPropagator);
        using var handler = new JiraCloudReader.SafeTransport(new JiraHandler((_, _) => throw new HttpRequestException("private-response opaque-secret")));
        using var http = new HttpClient(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, Base + "/rest/api/3/myself"); new JiraUserCredential("secret").Apply(request);
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => http.SendAsync(request));
        Assert.Equal("jira_transport_failed", ex.Message); Assert.Null(ex.InnerException);
    }

    [Fact]
    public async Task Provider_timeout_has_a_safe_504_and_retry_hint_is_bounded()
    {
        using var handler = new JiraHandler((_, _) => throw new OperationCanceledException("private-response opaque-secret"));
        using var http = new HttpClient(handler);
        var ex = await Assert.ThrowsAsync<OperationException>(() => Read(http));
        Assert.Equal(504, ex.Status); Assert.Equal("jira_timeout", ex.Message); Assert.Null(ex.InnerException);
        using var rateHandler = new JiraHandler((_, _) =>
        {
            var response = Json("private-response", HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new(TimeSpan.FromDays(100)); return Task.FromResult(response);
        });
        using var rateHttp = new HttpClient(rateHandler);
        Assert.Null((await Assert.ThrowsAsync<OperationException>(() => Read(rateHttp))).RetryAfterSeconds);
    }

    [Fact]
    public async Task Cancellation_interrupts_a_streamed_body_and_discards_private_errors()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new JiraHandler((_, _) =>
        {
            var content = new BlockingContent(entered); content.Headers.ContentType = new("application/json");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        });
        using var http = new HttpClient(handler); using var cts = new CancellationTokenSource();
        var pending = Read(http, cts.Token); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); cts.Cancel();
        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Null(ex.InnerException); Assert.DoesNotContain("private", ex.Message);
    }

    private sealed class UnknownLengthContent(string body) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(Encoding.UTF8.GetBytes(body)).AsTask();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(body)));
    }

    private sealed class BlockingContent(TaskCompletionSource entered) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new NotSupportedException();
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new BlockingStream(entered));
        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken ct) => CreateContentReadStreamAsync();
        private sealed class BlockingStream(TaskCompletionSource entered) : MemoryStream
        {
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            { entered.TrySetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); return 0; }
        }
    }
    private sealed class Verifier(VerifiedForgeIdentity identity) : IForgeIdentityVerifier
    { public Task<VerifiedForgeIdentity> VerifyAsync(string token, CancellationToken cancellationToken = default) => Task.FromResult(identity); }
    private sealed class Directory(TenantIdentity identity) : IInstallationDirectory
    { public Task<TenantIdentity?> ResolveAsync(VerifiedForgeIdentity verified, CancellationToken cancellationToken = default) => Task.FromResult<TenantIdentity?>(identity); }
}

internal sealed class JiraHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? respond = null) : HttpMessageHandler
{
    public System.Collections.Concurrent.ConcurrentQueue<string> Paths { get; } = new();
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Assert.Equal(HttpMethod.Get, request.Method); Assert.Equal("api.atlassian.com", request.RequestUri!.Host);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme); Assert.NotNull(request.Headers.Authorization?.Parameter);
        Paths.Enqueue(request.RequestUri.PathAndQuery);
        return respond is null ? Task.FromResult(Default(request)) : respond(request, ct);
    }
    internal static HttpResponseMessage Default(HttpRequestMessage request) => JiraProviderTests.Json(
        request.RequestUri!.AbsolutePath.EndsWith("myself", StringComparison.Ordinal) ? """{"accountId":"actor"}""" :
        request.RequestUri.Query.Contains("summary", StringComparison.Ordinal) ? JiraProviderTests.Full : JiraProviderTests.Metadata);
}
