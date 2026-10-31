using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Domain;

public enum AccessRole { Developer, Reviewer, EvaluationManager, SystemAdministrator }

/// <summary>Created from verified identity/installation binding; never from API payload fields.</summary>
public sealed record TenantIdentity
{
    public string TenantId { get; }
    public string OrganizationId { get; }
    public string ActorId { get; }
    public string CloudId { get; }
    public string InstallationId { get; }
    public string AppId { get; }
    public string EnvironmentId { get; }

    public TenantIdentity(string tenantId, string organizationId, string actorId, string cloudId,
        string installationId, string appId, string environmentId)
    {
        foreach (var value in new[] { tenantId, organizationId, actorId, cloudId, installationId, appId, environmentId })
            Require.That(!string.IsNullOrWhiteSpace(value) && value.Length <= 200 && !value.Any(char.IsControl),
                401, "invalid_tenant_identity");
        TenantId = tenantId; OrganizationId = organizationId; ActorId = actorId; CloudId = cloudId;
        InstallationId = installationId; AppId = appId; EnvironmentId = environmentId;
    }
}
public sealed record VerifiedForgeIdentity(string AppId, string EnvironmentId, string EnvironmentType,
    string InstallationId, string CloudId, string ActorId);

public sealed class OperationException(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public static class Require
{
    public static string Text(string? value, string code, int max = 200)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max || value.Any(char.IsControl))
            throw new OperationException(422, code);
        return value;
    }
    public static string Reason(string? value) => Text(value, "reason_required", 1000);
    public static void That([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, int status, string code)
    {
        if (!condition) throw new OperationException(status, code);
    }
    public static ProfileScope Scope(ProfileScope scope)
    {
        if (scope is null) throw new OperationException(422, "scope_required");
        That(Enum.IsDefined(scope.Type), 422, "invalid_scope");
        Text(scope.Id, "scope_id_required");
        return scope;
    }
}
