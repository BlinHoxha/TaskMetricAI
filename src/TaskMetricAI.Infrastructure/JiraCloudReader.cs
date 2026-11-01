using System.Net;
using System.Text;
using System.Text.Json;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;

namespace TaskMetricAI.Infrastructure;

/// <summary>No retries, redirects, provider logging, stored credentials or content caching.</summary>
public sealed class JiraCloudReader(HttpClient http) : IJiraReader
{
    public static HttpMessageHandler CreateTransport() => new SafeTransport(new SocketsHttpHandler
    {
        AllowAutoRedirect = false, UseCookies = false,
        // Suppress HttpClient DiagnosticListener payloads, which otherwise expose request/response objects.
        ActivityHeadersPropagator = null
    });

    internal sealed class SafeTransport(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            try { return await base.SendAsync(request, ct); }
            catch (OperationCanceledException) { throw new OperationCanceledException(ct); }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            { throw new HttpRequestException("jira_transport_failed"); }
        }
    }

    public async Task<JiraIssue> ReadIssueAsync(AuthenticatedTenant tenant, JiraUserCredential credential,
        string projectId, string issueId, CancellationToken cancellationToken = default)
    {
        JiraRequest.RequireId(projectId); JiraRequest.RequireId(issueId);
        var apiBase = JiraRequest.RequireBase(tenant);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var ct = deadline.Token;
        try
        {
            using var actor = await GetAsync(apiBase + "/rest/api/3/myself", credential, 65536, ct);
            Require.That(Text(actor.RootElement, "accountId", 200) == tenant.Identity.ActorId, 403, "jira_actor_denied");
            // Check project before retrieving issue content. Check again after retrieval to handle moved issues.
            var path = apiBase + "/rest/api/3/issue/" + issueId;
            using var metadata = await GetAsync(path + "?fields=project&updateHistory=false", credential, 65536, ct);
            CheckIssue(metadata.RootElement, projectId, issueId);
            using var document = await GetAsync(path + "?fields=project,summary,description,components&updateHistory=false", credential, 1_048_576, ct);
            var issue = document.RootElement;
            CheckIssue(issue, projectId, issueId);
            var fields = issue.GetProperty("fields");
            var components = fields.GetProperty("components");
            Require.That(components.ValueKind == JsonValueKind.Array && components.GetArrayLength() <= 100, 502, "jira_invalid_response");
            var ids = components.EnumerateArray().Select(c => Text(c, "id", 20)).ToArray();
            foreach (var id in ids) JiraRequest.RequireId(id);
            return new(issueId, Text(issue, "key", 200), projectId, Text(fields, "summary", 1024),
                Description(fields.GetProperty("description")), ids);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
            throw new OperationException(504, "jira_timeout");
        }
        catch (OperationException ex) when (ex.Status == 422)
        { throw new OperationException(502, "jira_invalid_response"); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or
            HttpRequestException or IOException or ArgumentException or FormatException)
        {
            // Do not retain inner exceptions: handlers/parsers may include credentials, URLs or private bodies.
            throw new OperationException(502, "jira_invalid_response");
        }
    }

    private async Task<JsonDocument> GetAsync(string url, JiraUserCredential credential, int maxBytes, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/json");
        credential.Apply(request);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var delta = response.Headers.RetryAfter?.Delta;
            var retry = delta is { TotalSeconds: >= 0 and <= 300 } ? (int?)Math.Ceiling(delta.Value.TotalSeconds) : null;
            throw new OperationException(429, "jira_rate_limited", retry);
        }
        Require.That(response.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound),
            403, "jira_access_denied");
        Require.That(response.StatusCode == HttpStatusCode.OK, 502, "jira_request_failed");
        Require.That(response.Content.Headers.ContentType?.MediaType == "application/json", 502, "jira_invalid_response");
        Require.That(response.Content.Headers.ContentLength is null || response.Content.Headers.ContentLength <= maxBytes,
            502, "jira_response_too_large");
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(block, ct)) != 0)
        {
            Require.That(buffer.Length + count <= maxBytes, 502, "jira_response_too_large");
            await buffer.WriteAsync(block.AsMemory(0, count), ct);
        }
        var result = JsonDocument.Parse(buffer.ToArray(), new() { MaxDepth = 32 });
        try { UniqueProperties(result.RootElement); return result; }
        catch { result.Dispose(); throw; }
    }

    private static void UniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                Require.That(names.Add(property.Name), 502, "jira_invalid_response");
                UniqueProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var child in value.EnumerateArray()) UniqueProperties(child);
    }

    private static string Text(JsonElement value, string name, int max)
    {
        var text = value.GetProperty(name).GetString();
        Require.That(text is not null && text.Length <= max, 502, "jira_invalid_response");
        return text;
    }

    private static void CheckIssue(JsonElement issue, string projectId, string issueId)
    {
        Require.That(Text(issue, "id", 20) == issueId &&
            Text(issue.GetProperty("fields").GetProperty("project"), "id", 20) == projectId, 403, "jira_issue_denied");
    }

    private static string? Description(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        Require.That(Text(value, "type", 40) == "doc" && value.GetProperty("version").GetInt32() == 1,
            502, "jira_invalid_response");
        var output = new StringBuilder();
        var nodes = 0;
        Visit(value);
        return output.ToString().TrimEnd('\n');

        void Visit(JsonElement node)
        {
            Require.That(++nodes <= 10000, 502, "jira_response_too_large");
            var type = Text(node, "type", 40);
            if (type == "text") output.Append(Text(node, "text", 32768));
            else if (type == "hardBreak") output.Append('\n');
            else if (type is "doc" or "paragraph" or "heading" or "bulletList" or "orderedList" or "listItem" or
                "blockquote" or "codeBlock" or "table" or "tableRow" or "tableCell" or "tableHeader" or "panel" or "expand" or "nestedExpand")
            {
                var children = node.GetProperty("content");
                Require.That(children.ValueKind == JsonValueKind.Array, 502, "jira_invalid_response");
                foreach (var child in children.EnumerateArray()) Visit(child);
                if (type is "paragraph" or "heading" or "codeBlock") output.Append('\n');
            }
            // Unsupported nodes/attributes (mentions, media, cards, marks/links) are never copied or fetched.
            Require.That(output.Length <= 32768, 502, "jira_response_too_large");
        }
    }
}
