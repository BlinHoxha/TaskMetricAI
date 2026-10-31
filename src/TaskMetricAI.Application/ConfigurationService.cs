using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Application;

/// <summary>Tenant-bound configuration operations; one service/repository unit of work per request.</summary>
public sealed class ConfigurationService(ITenantRepository repository, TenantIdentity identity, TimeProvider clock)
{
    private readonly AccessControl access = new(repository, identity);

    public async Task<MetricDefinition> AddMetricVersionAsync(string metricId, MetricInput input, int expectedVersion,
        CancellationToken ct = default)
    {
        await access.RequireAdministratorAsync(ct);
        if (input is null) throw new OperationException(422, "metric_input_required");
        Require.Text(metricId, "metric_id_required");
        Require.Reason(input.Reason);
        var existing = await repository.FindAsync<MetricVersion>(x => x.MetricId == metricId, ct);
        var latest = existing.Count == 0 ? 0 : existing.Max(x => x.Version);
        Require.That(latest == expectedVersion, 409, "metric_version_conflict");
        var metric = new MetricDefinition(identity.TenantId, metricId, checked(latest + 1), MetricKind.Custom,
            input.Label, input.Description, input.Anchors);
        repository.Add(new MetricVersion { TenantId = identity.TenantId, MetricId = metricId, Version = metric.Version,
            DefinitionJson = Documents.Write(MetricDocument.From(metric)), CreatedBy = identity.ActorId, CreatedAt = clock.GetUtcNow() });
        Audit("metric.version_created", metricId, input.Reason, targetType: "metric", targetVersion: metric.Version,
            afterState: new { metric.MetricId, metric.Version, metric.Kind });
        await repository.SaveAsync(ct);
        return metric;
    }

    public async Task<CatalogVersion> AddCatalogVersionAsync(string catalogId, MetricReference[] selection,
        int expectedVersion, string reason, CancellationToken ct = default)
    {
        await access.RequireAdministratorAsync(ct);
        Require.Text(catalogId, "catalog_id_required"); Require.Reason(reason);
        Require.That(selection is not null && selection.All(item => item is not null), 422, "invalid_catalog_selection");
        Require.That(selection.All(x => x.TenantId == identity.TenantId), 403, "cross_tenant_metric");
        var old = await repository.FindAsync<CatalogVersion>(x => x.CatalogId == catalogId, ct);
        var latest = old.Count == 0 ? 0 : old.Max(x => x.Version);
        Require.That(latest == expectedVersion, 409, "catalog_version_conflict");
        var rows = new List<MetricVersion>();
        foreach (var item in selection)
        {
            var row = (await repository.FindAsync<MetricVersion>(x => x.MetricId == item.MetricId && x.Version == item.Version, ct)).SingleOrDefault();
            Require.That(row is not null, 404, "metric_version_not_found");
            rows.Add(row!);
        }
        var catalog = new MetricCatalog(identity.TenantId, rows.Select(MetricDocuments.Restore));
        var version = new CatalogVersion { TenantId = identity.TenantId, CatalogId = catalogId, Version = checked(latest + 1),
            SnapshotJson = Documents.Write(catalog.Definitions.Select(MetricDocument.From).ToArray()),
            CreatedBy = identity.ActorId, CreatedAt = clock.GetUtcNow() };
        repository.Add(version);
        foreach (var row in rows) repository.Add(new CatalogEntry { TenantId = identity.TenantId,
            CatalogVersionId = version.Id, MetricVersionId = row.Id, MetricId = row.MetricId });
        Audit("catalog.version_created", version.Id, reason, targetType: "catalog", targetVersion: version.Version,
            afterState: new { version.CatalogId, version.Version, Metrics = selection });
        await repository.SaveAsync(ct);
        return version;
    }

    public async Task<MetricCatalog> ReadCatalogAsync(string id, int version, CancellationToken ct = default)
    {
        // Catalogs are organization-wide; scope managers can instead embed custom definitions in their profiles.
        await access.RequireScopeAsync(new(ScopeType.Organization, identity.OrganizationId), false, ct);
        var row = (await repository.FindAsync<CatalogVersion>(x => x.CatalogId == id && x.Version == version, ct)).SingleOrDefault();
        Require.That(row is not null, 404, "catalog_not_found");
        return new(identity.TenantId, Documents.Read<MetricDocument[]>(row!.SnapshotJson).Select(x => x.Restore()));
    }

    public async Task<EvaluationProfile> AddProfileVersionAsync(string profileId, ProfileInput input, int expectedVersion,
        CancellationToken ct = default)
    {
        if (input is null) throw new OperationException(422, "profile_input_required");
        await access.RequireScopeAsync(input.Scope, true, ct);
        Require.Text(profileId, "profile_id_required"); Require.Reason(input.Reason);
        Require.That(input.Criteria is not null && input.Criteria.All(item => item is not null) &&
            input.ComplexityBands is not null && input.ComplexityBands.All(item => item is not null) &&
            (input.StoryPointBands is null || input.StoryPointBands.All(item => item is not null)) &&
            input.Policy is not null, 422, "invalid_profile_configuration");
        var old = await repository.FindAsync<ProfileVersion>(x => x.ProfileId == profileId, ct);
        Require.That(old.All(x => x.ScopeType == input.Scope.Type && x.ScopeId == input.Scope.Id), 403, "profile_scope_mismatch");
        var latest = old.Count == 0 ? 0 : old.Max(x => x.Version);
        Require.That(latest == expectedVersion, 409, "profile_version_conflict");
        var profile = new EvaluationProfile(identity.TenantId, profileId, checked(latest + 1), input.Name,
            input.Description, input.Scope, input.Criteria, input.ComplexityBands,
            new(identity.ActorId, clock.GetUtcNow(), input.Reason), input.StoryPointBands, input.Policy);
        foreach (var criterion in profile.Criteria.Where(x => x.SourceMetric is not null))
        {
            var source = criterion.SourceMetric!;
            var row = (await repository.FindAsync<MetricVersion>(x => x.MetricId == source.MetricId && x.Version == source.Version, ct)).SingleOrDefault();
            Require.That(row is not null, 404, "metric_version_not_found");
            var metric = MetricDocuments.Restore(row!);
            Require.That(criterion.Label == metric.Label && criterion.Definition == metric.Description && criterion.Anchors == metric.Anchors,
                422, "metric_provenance_mismatch");
        }
        repository.Add(new ProfileVersion { TenantId = identity.TenantId, ProfileId = profileId, Version = profile.Version,
            ScopeType = profile.Scope.Type, ScopeId = profile.Scope.Id, Status = ProfileStatus.Draft,
            ContentHash = profile.ContentHash, SnapshotJson = Documents.Write(ProfileDocument.From(profile)),
            CreatedBy = identity.ActorId, CreatedAt = clock.GetUtcNow() });
        Audit("profile.version_created", profileId, input.Reason, after: profile.ContentHash,
            targetType: "profile", targetVersion: profile.Version, scope: profile.Scope,
            afterState: new { profile.Version, profile.Status, profile.ContentHash });
        await repository.SaveAsync(ct);
        return profile;
    }

    public async Task<EvaluationProfile> ReadProfileAsync(string profileId, int version, CancellationToken ct = default)
    {
        var row = await FindProfile(profileId, version, ct);
        await access.RequireScopeAsync(new(row.ScopeType, row.ScopeId), false, ct);
        return Documents.Restore(row);
    }

    public async Task TransitionAsync(string profileId, int version, ProfileStatus status, long expectedRevision,
        string reason, CancellationToken ct = default)
    {
        var row = await FindProfile(profileId, version, ct);
        await access.RequireScopeAsync(new(row.ScopeType, row.ScopeId), true, ct);
        Require.Reason(reason);
        Require.That(row.Revision == expectedRevision, 409, "profile_revision_conflict");
        Require.That((row.Status, status) is (ProfileStatus.Draft, ProfileStatus.Validated) or
            (ProfileStatus.Validated, ProfileStatus.Active) or (ProfileStatus.Active, ProfileStatus.Archived), 409, "invalid_profile_transition");
        _ = Documents.Restore(row);
        if (status == ProfileStatus.Active)
        {
            var active = await repository.FindAsync<ProfileVersion>(x => x.ScopeType == row.ScopeType &&
                x.ScopeId == row.ScopeId && x.Status == ProfileStatus.Active, ct);
            Require.That(active.Count == 0, 409, "scope_already_active");
        }
        var beforeStatus = row.Status;
        row.Status = status;
        if (status == ProfileStatus.Archived)
            foreach (var assignment in await repository.FindAsync<ProfileAssignment>(x => x.ProfileVersionId == row.Id, ct))
                repository.Remove(assignment);
        Audit("profile." + status.ToString().ToLowerInvariant(), row.Id, reason, row.ContentHash, row.ContentHash,
            targetType: "profile", targetVersion: row.Version, scope: new(row.ScopeType, row.ScopeId),
            beforeState: new { Status = beforeStatus, row.Revision },
            afterState: new { Status = status, Revision = checked(row.Revision + 1), DetachAssignments = status == ProfileStatus.Archived });
        await repository.SaveAsync(ct);
    }

    public async Task AssignAsync(ProfileScope scope, string profileId, int version, string reason, CancellationToken ct = default)
    {
        await access.RequireScopeAsync(scope, true, ct); Require.Reason(reason);
        var row = await FindProfile(profileId, version, ct);
        // Exact scope only in this increment; broader-scope profile reuse needs a separately authorized policy.
        Require.That(row.ScopeType == scope.Type && row.ScopeId == scope.Id, 403, "profile_scope_mismatch");
        Require.That(row.Status == ProfileStatus.Active, 409, "profile_not_active");
        var existing = (await repository.FindAsync<ProfileAssignment>(x => x.ScopeType == scope.Type && x.ScopeId == scope.Id, ct)).SingleOrDefault();
        var beforeAssignment = existing?.ProfileVersionId;
        if (existing is null) repository.Add(new ProfileAssignment { TenantId = identity.TenantId,
            ScopeType = scope.Type, ScopeId = scope.Id, ProfileVersionId = row.Id });
        else existing.ProfileVersionId = row.Id;
        Audit("profile.assigned", row.Id, reason, after: row.ContentHash, targetType: "assignment", targetVersion: row.Version,
            scope: scope, beforeState: new { ProfileVersionId = beforeAssignment }, afterState: new { ProfileVersionId = row.Id });
        await repository.SaveAsync(ct);
    }

    public async Task GrantAsync(string actorId, AccessRole role, ProfileScope scope, string reason, CancellationToken ct = default)
    {
        await repository.LockAuthorizationAsync(ct);
        await access.RequireAdministratorAsync(ct); Require.Scope(scope); Require.Reason(reason); Require.Text(actorId, "actor_required");
        await access.RequireRegisteredScopeAsync(scope, ct);
        Require.That(Enum.IsDefined(role), 422, "invalid_role");
        Require.That(scope.Type != ScopeType.Organization || scope.Id == identity.OrganizationId, 403, "scope_denied");
        Require.That(role != AccessRole.SystemAdministrator || scope.Type == ScopeType.Organization, 422, "administrator_scope_required");
        repository.Add(new ScopeGrant { TenantId = identity.TenantId, ActorId = actorId, Role = role,
            ScopeType = scope.Type, ScopeId = scope.Id });
        Audit("scope.granted", actorId, reason, targetType: "scope_grant", scope: scope,
            targetActor: actorId, beforeState: new { Granted = false }, afterState: new { Granted = true, Role = role, Scope = scope });
        await repository.SaveAsync(ct);
    }

    public async Task RevokeGrantAsync(string grantId, string reason, CancellationToken ct = default)
    {
        // Serialize grant creation/revocation before reading authority or counting administrators.
        await repository.LockAuthorizationAsync(ct);
        await access.RequireAdministratorAsync(ct); Require.Reason(reason); Require.Text(grantId, "grant_id_required");
        var grant = (await repository.FindAsync<ScopeGrant>(x => x.TenantId == identity.TenantId && x.Id == grantId, ct)).SingleOrDefault()
            ?? throw new OperationException(404, "grant_not_found");
        if (grant.Role == AccessRole.SystemAdministrator)
        {
            var administrators = await repository.FindAsync<ScopeGrant>(x => x.TenantId == identity.TenantId &&
                x.Role == AccessRole.SystemAdministrator && x.ScopeType == ScopeType.Organization && x.ScopeId == identity.OrganizationId, ct);
            Require.That(administrators.Any(x => x.Id != grant.Id), 409, "final_administrator_required");
        }
        repository.Remove(grant);
        Audit("scope.revoked", grant.Id, reason, targetType: "scope_grant", scope: new(grant.ScopeType, grant.ScopeId),
            targetActor: grant.ActorId, beforeState: new { Granted = true, grant.Role }, afterState: new { Granted = false });
        await repository.SaveAsync(ct);
    }

    public async Task<ResolvedProfile> ResolveProfileAsync(string projectId, CancellationToken ct = default)
    {
        Require.Text(projectId, "project_id_required");
        await access.RequireScopeAsync(new(ScopeType.JiraProject, projectId), false, ct);
        // One repository statement snapshots mappings, assignments and profile content together.
        var candidates = await repository.ProfileCandidatesAsync(projectId, identity.OrganizationId, ct);
        var selected = candidates.Where(x => x.ScopeType == ScopeType.JiraProject).ToArray();
        if (selected.Length == 0) selected = candidates.Where(x => x.ScopeType == ScopeType.Team).ToArray();
        if (selected.Length == 0) selected = candidates.Where(x => x.ScopeType == ScopeType.Organization).ToArray();
        Require.That(selected.Length > 0, 404, "profile_configuration_missing");
        Require.That(selected.Length == 1, 409, "profile_selection_ambiguous");
        var candidate = selected[0]; var row = candidate.Profile;
        Require.That(row is not null && row.TenantId == identity.TenantId && row.Status == ProfileStatus.Active &&
            row.ScopeType == candidate.ScopeType && row.ScopeId == candidate.ScopeId, 409, "profile_assignment_invalid");
        // Project entitlement permits this resolved snapshot, not arbitrary reads of its broader scope.
        return new(projectId, new(candidate.ScopeType, candidate.ScopeId), Documents.Restore(row));
    }

    private async Task<ProfileVersion> FindProfile(string id, int version, CancellationToken ct) =>
        (await repository.FindAsync<ProfileVersion>(x => x.ProfileId == id && x.Version == version, ct)).SingleOrDefault()
        ?? throw new OperationException(404, "profile_not_found");

    private void Audit(string operation, string subject, string reason, string? before = null, string? after = null,
        string targetType = "configuration", int? targetVersion = null, ProfileScope? scope = null,
        string? targetActor = null, object? beforeState = null, object? afterState = null) =>
        repository.Add(new AuditEvent { TenantId = identity.TenantId, ActorId = identity.ActorId, Operation = operation,
            SubjectId = subject, Reason = reason, BeforeHash = before, AfterHash = after, OccurredAt = clock.GetUtcNow(),
            TargetType = targetType, TargetVersion = targetVersion, TargetActorId = targetActor,
            ScopeType = scope?.Type ?? ScopeType.Organization, ScopeId = scope?.Id ?? identity.OrganizationId,
            BeforeStateJson = beforeState is null ? null : Documents.Write(beforeState),
            AfterStateJson = afterState is null ? null : Documents.Write(afterState) });
}

public sealed record ResolvedProfile(string ProjectId, ProfileScope SelectedScope, EvaluationProfile Profile);
