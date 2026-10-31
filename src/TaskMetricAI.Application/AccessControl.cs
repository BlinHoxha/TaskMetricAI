using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Application;

public sealed class AccessControl
{
    private readonly ITenantRepository repository;
    private readonly TenantIdentity identity;

    public AccessControl(ITenantRepository repository, TenantIdentity identity)
    {
        Require.That(repository.TenantId == identity.TenantId, 403, "tenant_context_mismatch");
        this.repository = repository;
        this.identity = identity;
    }

    public async Task<bool> IsAdministratorAsync(CancellationToken ct = default) =>
        (await repository.FindAsync<ScopeGrant>(g => g.TenantId == identity.TenantId && g.ActorId == identity.ActorId &&
            g.Role == AccessRole.SystemAdministrator && g.ScopeType == ScopeType.Organization &&
            g.ScopeId == identity.OrganizationId, ct)).Count != 0;

    public async Task RequireAdministratorAsync(CancellationToken ct = default) =>
        Require.That(await IsAdministratorAsync(ct), 403, "administrator_required");

    public async Task RequireScopeAsync(ProfileScope scope, bool manage, CancellationToken ct = default)
    {
        Require.Scope(scope);
        Require.That(scope.Type != ScopeType.Organization || scope.Id == identity.OrganizationId, 403, "scope_denied");
        await RequireRegisteredScopeAsync(scope, ct);
        if (await IsAdministratorAsync(ct)) return;
        var grants = await repository.FindAsync<ScopeGrant>(g => g.TenantId == identity.TenantId && g.ActorId == identity.ActorId &&
            ((g.ScopeType == scope.Type && g.ScopeId == scope.Id) ||
             (!manage && g.ScopeType == ScopeType.Organization && g.ScopeId == identity.OrganizationId)), ct);
        Require.That(grants.Any(g => manage ? g.Role == AccessRole.EvaluationManager :
            g.Role == AccessRole.Developer || g.Role == AccessRole.Reviewer || g.Role == AccessRole.EvaluationManager),
            403, "scope_denied");
    }

    public async Task RequireRegisteredScopeAsync(ProfileScope scope, CancellationToken ct = default)
    {
        Require.Scope(scope);
        var registered = scope.Type switch
        {
            ScopeType.Organization => scope.Id == identity.OrganizationId,
            ScopeType.JiraProject => (await repository.FindAsync<ProjectRegistration>(x => x.TenantId == identity.TenantId && x.Id == scope.Id, ct)).Count == 1,
            ScopeType.Team => (await repository.FindAsync<TeamRegistration>(x => x.TenantId == identity.TenantId && x.Id == scope.Id, ct)).Count == 1,
            _ => false
        };
        Require.That(registered, 403, "scope_not_registered");
    }

    public async Task RequireRepositoryAsync(string connectionId, CancellationToken ct = default) =>
        Require.That((await repository.FindAsync<RepositoryAccess>(g => g.TenantId == identity.TenantId && g.ActorId == identity.ActorId &&
            g.RepositoryConnectionId == connectionId, ct)).Count != 0, 403, "repository_access_denied");
}
