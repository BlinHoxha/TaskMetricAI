using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Application;

public sealed class AccessControl(ITenantRepository repository, TenantIdentity identity)
{
    public async Task<bool> IsAdministratorAsync(CancellationToken ct = default) =>
        (await repository.FindAsync<ScopeGrant>(g => g.ActorId == identity.ActorId &&
            g.Role == AccessRole.SystemAdministrator && g.ScopeType == ScopeType.Organization &&
            g.ScopeId == identity.OrganizationId, ct)).Count != 0;

    public async Task RequireAdministratorAsync(CancellationToken ct = default) =>
        Require.That(await IsAdministratorAsync(ct), 403, "administrator_required");

    public async Task RequireScopeAsync(ProfileScope scope, bool manage, CancellationToken ct = default)
    {
        Require.Scope(scope);
        Require.That(scope.Type != ScopeType.Organization || scope.Id == identity.OrganizationId, 403, "scope_denied");
        if (await IsAdministratorAsync(ct)) return;
        var grants = await repository.FindAsync<ScopeGrant>(g => g.ActorId == identity.ActorId &&
            ((g.ScopeType == scope.Type && g.ScopeId == scope.Id) ||
             (!manage && g.ScopeType == ScopeType.Organization && g.ScopeId == identity.OrganizationId)), ct);
        Require.That(grants.Any(g => !manage || g.Role == AccessRole.EvaluationManager), 403, "scope_denied");
    }

    public async Task RequireRepositoryAsync(string connectionId, CancellationToken ct = default) =>
        Require.That((await repository.FindAsync<RepositoryAccess>(g => g.ActorId == identity.ActorId &&
            g.RepositoryConnectionId == connectionId, ct)).Count != 0, 403, "repository_access_denied");
}
