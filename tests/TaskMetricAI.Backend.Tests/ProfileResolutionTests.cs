using System.Linq.Expressions;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Backend.Tests;

public sealed class ProfileResolutionTests
{
    [Theory]
    [InlineData("inactive")]
    [InlineData("foreign")]
    [InlineData("missing")]
    [InlineData("wrong-scope")]
    [InlineData("hash")]
    [InlineData("schema")]
    public async Task Invalid_explicit_assignment_never_silently_falls_back(string invalid)
    {
        var row = PersistenceTests.ProfileRow("a"); row.Status = ProfileStatus.Active;
        var store = new CandidateStore([new(ScopeType.JiraProject, "project", row),
            new(ScopeType.Organization, "org", PersistenceTests.ProfileRow("a"))]);
        // Build a valid project-scoped stored document first, then simulate retained corrupt storage.
        var input = PersistenceTests.Input(scope: new(ScopeType.JiraProject, "project"));
        var profile = new EvaluationProfile("a", "project-profile", 1, input.Name, input.Description, input.Scope,
            input.Criteria, input.ComplexityBands, new("actor", DateTimeOffset.UtcNow, "test"));
        row.ScopeType = ScopeType.JiraProject; row.ScopeId = "project"; row.ProfileId = profile.ProfileId;
        row.ContentHash = profile.ContentHash; row.SnapshotJson = Documents.Write(ProfileDocument.From(profile));
        if (invalid == "inactive") row.Status = ProfileStatus.Draft;
        if (invalid == "foreign") row.TenantId = "b";
        if (invalid == "missing") store.Candidates[0] = new(ScopeType.JiraProject, "project", null);
        if (invalid == "wrong-scope") row.ScopeId = "other";
        if (invalid == "hash") row.ContentHash = "tampered";
        if (invalid == "schema") row.SnapshotJson = row.SnapshotJson.Replace("profile-document-v2", "unknown-schema", StringComparison.Ordinal);
        var service = new ConfigurationService(store, AuthorizationTests.Identity(), TimeProvider.System);
        var denied = await Assert.ThrowsAsync<OperationException>(() => service.ResolveProfileAsync("project"));
        Assert.Equal(409, denied.Status);
    }

    private sealed class CandidateStore(ProfileCandidate[] candidates) : ITenantRepository
    {
        private readonly MemoryStore memory = Build();
        public ProfileCandidate[] Candidates { get; } = candidates;
        public string TenantId => "a";
        private static MemoryStore Build()
        {
            var result = new MemoryStore("a"); result.Add(PersistenceTests.Grant("a"));
            result.Add(new ProjectRegistration { TenantId = "a", Id = "project" }); return result;
        }
        public Task<IReadOnlyList<T>> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : TenantEntity =>
            memory.FindAsync(predicate, cancellationToken);
        public Task<IReadOnlyList<ProfileCandidate>> ProfileCandidatesAsync(string projectId, string organizationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProfileCandidate>>(Candidates);
        public void Add<T>(T entity) where T : TenantEntity => throw new NotSupportedException();
        public void Remove<T>(T entity) where T : TenantEntity => throw new NotSupportedException();
        public Task SaveAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
