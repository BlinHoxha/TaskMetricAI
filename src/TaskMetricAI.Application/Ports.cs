using System.Linq.Expressions;
using System.Text.Json;
using TaskMetricAI.Domain;

namespace TaskMetricAI.Application;

public interface ITenantRepository
{
    string TenantId { get; }
    Task<IReadOnlyList<T>> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : TenantEntity;
    void Add<T>(T entity) where T : TenantEntity;
    void Remove<T>(T entity) where T : TenantEntity;
    Task SaveAsync(CancellationToken cancellationToken = default);
    Task LockAuthorizationAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Authorization mutation requires serialization.");
    Task<IReadOnlyList<ProfileCandidate>> ProfileCandidatesAsync(string projectId, string organizationId,
        CancellationToken cancellationToken = default) => throw new NotSupportedException("Profile resolution requires a consistent repository query.");
}
public sealed record ProfileCandidate(TaskMetricAI.Evaluation.ScopeType ScopeType, string ScopeId, ProfileVersion? Profile);
public interface IInstallationDirectory
{
    Task<TenantIdentity?> ResolveAsync(VerifiedForgeIdentity identity, CancellationToken cancellationToken = default);
}
public interface IForgeIdentityVerifier
{
    Task<VerifiedForgeIdentity> VerifyAsync(string token, CancellationToken cancellationToken = default);
}
public interface IJiraReader
{
    Task<JiraIssue> ReadIssueAsync(AuthenticatedTenant tenant, JiraUserCredential credential, string projectId,
        string issueId, CancellationToken cancellationToken = default);
}
public sealed record GitHubRepository(long Id, string Owner, string Name, string DefaultBranch);
public interface IGitHubReader
{
    Task<GitHubRepository> ReadRepositoryAsync(RepositoryConnection connection, CancellationToken cancellationToken = default);
    Task<string> ResolveCommitAsync(RepositoryConnection connection, string reference, CancellationToken cancellationToken = default);
    Task<JsonElement> ReadTreeAsync(RepositoryConnection connection, string commitSha, CancellationToken cancellationToken = default);
}
