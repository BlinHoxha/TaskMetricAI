using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Domain;

public enum AccessRole { Developer, Reviewer, EvaluationManager, SystemAdministrator }

/// <summary>Created from verified identity/installation binding; never from API payload fields.</summary>
public sealed record TenantIdentity(string TenantId, string OrganizationId, string ActorId, string CloudId,
    string InstallationId, string AppId, string EnvironmentId);
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
    public static void That(bool condition, int status, string code)
    {
        if (!condition) throw new OperationException(status, code);
    }
    public static ProfileScope Scope(ProfileScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        That(Enum.IsDefined(scope.Type), 422, "invalid_scope");
        Text(scope.Id, "scope_id_required");
        return scope;
    }
}
