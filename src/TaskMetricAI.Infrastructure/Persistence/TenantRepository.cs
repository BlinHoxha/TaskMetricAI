using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;

namespace TaskMetricAI.Infrastructure.Persistence;

public sealed class TenantRepository(ConfigurationDbContext db) : ITenantRepository
{
    public string TenantId => db.TenantId;
    public async Task<IReadOnlyList<T>> FindAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : TenantEntity =>
        await db.Set<T>().Where(predicate).ToArrayAsync(cancellationToken);
    public void Add<T>(T entity) where T : TenantEntity => db.Set<T>().Add(entity);
    public void Remove<T>(T entity) where T : TenantEntity => db.Set<T>().Remove(entity);
    public async Task LockAuthorizationAsync(CancellationToken cancellationToken = default)
    {
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({db.TenantId}, 7621))", cancellationToken);
    }
    public async Task<IReadOnlyList<ProfileCandidate>> ProfileCandidatesAsync(string projectId, string organizationId,
        CancellationToken cancellationToken = default)
    {
        var teams = db.Set<ProjectTeam>().Where(x => x.ProjectId == projectId).Select(x => x.TeamId);
        var assignments = db.Set<ProfileAssignment>().Where(x =>
            (x.ScopeType == TaskMetricAI.Evaluation.ScopeType.JiraProject && x.ScopeId == projectId) ||
            (x.ScopeType == TaskMetricAI.Evaluation.ScopeType.Team && teams.Contains(x.ScopeId)) ||
            (x.ScopeType == TaskMetricAI.Evaluation.ScopeType.Organization && x.ScopeId == organizationId));
        return await (from assignment in assignments
                      join profile in db.Set<ProfileVersion>() on new { assignment.TenantId, Id = assignment.ProfileVersionId }
                          equals new { profile.TenantId, profile.Id } into profiles
                      from profile in profiles.DefaultIfEmpty()
                      select new ProfileCandidate(assignment.ScopeType, assignment.ScopeId, profile))
            .AsNoTracking().ToArrayAsync(cancellationToken);
    }
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new OperationException(409, "configuration_conflict"); }
        catch (DbUpdateException) { throw new OperationException(409, "configuration_constraint_conflict"); }
    }
}
