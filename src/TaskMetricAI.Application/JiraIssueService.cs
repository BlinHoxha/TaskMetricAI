using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Application;

// Deliberately no provider JSON, URLs, comments, user objects or custom fields.
public sealed record JiraIssue(string Id, string Key, string ProjectId, string Summary,
    string? DescriptionText, string[] ComponentIds)
{
    public override string ToString() => "JiraIssue [redacted]";
}

/// <summary>Request-owned opaque credential; never register as a singleton or serialize it.</summary>
public sealed class JiraUserCredential
{
    private readonly string value;
    public JiraUserCredential(string value)
    {
        Require.That(!string.IsNullOrEmpty(value) && value.Length <= 16384 &&
            value.All(c => c is >= '!' and <= '~' && c != ','), 401, "jira_user_token_required");
        this.value = value;
    }
    public void Apply(System.Net.Http.HttpRequestMessage request) =>
        request.Headers.Authorization = new("Bearer", value);
    public override string ToString() => "JiraUserCredential [redacted]";
}

public static class JiraRequest
{
    public static void RequireId(string value) => Require.That(value is { Length: > 0 and <= 20 } &&
        value[0] is >= '1' and <= '9' && value.All(c => c is >= '0' and <= '9'), 422, "invalid_jira_id");

    public static string RequireBase(AuthenticatedTenant tenant)
    {
        var expected = "https://api.atlassian.com/ex/jira/" + tenant.Identity.CloudId;
        Require.That(tenant.ApiBaseUrl == expected, 403, "jira_api_base_denied");
        return expected;
    }
}

public sealed class JiraIssueService(ITenantRepository repository, IJiraReader reader)
{
    public async Task<JiraIssue> ReadAsync(AuthenticatedTenant tenant, JiraUserCredential credential,
        string projectId, string issueId, CancellationToken ct = default)
    {
        JiraRequest.RequireId(projectId); JiraRequest.RequireId(issueId);
        await new AccessControl(repository, tenant.Identity).RequireScopeAsync(new(ScopeType.JiraProject, projectId), false, ct);
        JiraRequest.RequireBase(tenant);
        var issue = await reader.ReadIssueAsync(tenant, credential, projectId, issueId, ct);
        Require.That(issue.ProjectId == projectId && issue.Id == issueId, 403, "jira_issue_denied");
        return issue;
    }
}
