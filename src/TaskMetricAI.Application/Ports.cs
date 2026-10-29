using System.Linq.Expressions;
using System.Text.Json;
using TaskMetricAI.Domain;

namespace TaskMetricAI.Application;

public interface ITenantRepository
{
    Task<IReadOnlyList<T>> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : TenantEntity;
    void Add<T>(T entity) where T : TenantEntity;
    void Remove<T>(T entity) where T : TenantEntity;
    Task SaveAsync(CancellationToken cancellationToken = default);
}
public interface IInstallationDirectory
{
    Task<TenantIdentity?> ResolveAsync(VerifiedForgeIdentity identity, CancellationToken cancellationToken = default);
}
public interface IForgeIdentityVerifier
{
    Task<VerifiedForgeIdentity> VerifyAsync(string token, CancellationToken cancellationToken = default);
}
public sealed record JiraIssue(string Id, string Key, string ProjectId, string[] ComponentIds,
    JsonElement Fields, JsonElement[] Comments);
public interface IJiraReader
{
    Task<JiraIssue> ReadIssueAsync(TenantIdentity identity, string userToken, string issueId, CancellationToken cancellationToken = default);
}
public sealed record GitHubRepository(long Id, string Owner, string Name, string DefaultBranch);
public interface IGitHubReader
{
    Task<GitHubRepository> ReadRepositoryAsync(RepositoryConnection connection, CancellationToken cancellationToken = default);
    Task<string> ResolveCommitAsync(RepositoryConnection connection, string reference, CancellationToken cancellationToken = default);
    Task<JsonElement> ReadTreeAsync(RepositoryConnection connection, string commitSha, CancellationToken cancellationToken = default);
}
