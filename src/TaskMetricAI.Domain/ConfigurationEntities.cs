using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Domain;

public abstract class TenantEntity
{
    public string TenantId { get; set; } = "";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
}

// Separate installation directory: only exact verified identities may select a tenant.
public sealed class TenantRegistration
{
    public string TenantId { get; set; } = "";
    public string OrganizationId { get; set; } = "";
}

public sealed class TenantInstallation
{
    public string InstallationId { get; set; } = "";
    public string TenantId { get; set; } = "";
    public string OrganizationId { get; set; } = "";
    public string AppId { get; set; } = "";
    public string EnvironmentId { get; set; } = "";
    public string CloudId { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed class ScopeGrant : TenantEntity
{
    public string ActorId { get; set; } = "";
    public ScopeType ScopeType { get; set; }
    public string ScopeId { get; set; } = "";
    public AccessRole Role { get; set; }
}
public sealed class MetricVersion : TenantEntity
{
    public string MetricId { get; set; } = "";
    public int Version { get; set; }
    public string DefinitionJson { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class CatalogVersion : TenantEntity
{
    public string CatalogId { get; set; } = "";
    public int Version { get; set; }
    public string SnapshotJson { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class CatalogEntry : TenantEntity
{
    public string CatalogVersionId { get; set; } = "";
    public string MetricVersionId { get; set; } = "";
    public string MetricId { get; set; } = "";
}
public sealed class ProfileVersion : TenantEntity
{
    public string ProfileId { get; set; } = "";
    public int Version { get; set; }
    public ScopeType ScopeType { get; set; }
    public string ScopeId { get; set; } = "";
    public string SnapshotJson { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public ProfileStatus Status { get; set; }
    public long Revision { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class ProfileAssignment : TenantEntity
{
    public long Revision { get; set; }
    public ScopeType ScopeType { get; set; }
    public string ScopeId { get; set; } = "";
    public string ProfileVersionId { get; set; } = "";
}
public sealed class ProjectTeam : TenantEntity
{
    public string ProjectId { get; set; } = "";
    public string TeamId { get; set; } = "";
}
public sealed class ProjectRegistration : TenantEntity { }
public sealed class TeamRegistration : TenantEntity { }
public sealed class RepositoryConnection : TenantEntity
{
    public long InstallationId { get; set; }
    public long RepositoryId { get; set; }
    public string Owner { get; set; } = "";
    public string Name { get; set; } = "";
}
public sealed class RepositoryAccess : TenantEntity
{
    public string ActorId { get; set; } = "";
    public string RepositoryConnectionId { get; set; } = "";
}
public sealed class RepositoryMapping : TenantEntity
{
    public string ProjectId { get; set; } = "";
    public string? ComponentId { get; set; }
    public string? IssueId { get; set; }
    public string RepositoryConnectionId { get; set; } = "";
    public string Branch { get; set; } = "";
}
public sealed class PinnedRepositorySnapshot : TenantEntity
{
    public string MappingId { get; set; } = "";
    public string RepositoryConnectionId { get; set; } = "";
    public string IssueId { get; set; } = "";
    public string CommitSha { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
public sealed class AuditEvent : TenantEntity
{
    public string TargetType { get; set; } = "legacy";
    public int? TargetVersion { get; set; }
    public string? TargetActorId { get; set; }
    public ScopeType? ScopeType { get; set; }
    public string? ScopeId { get; set; }
    public string? BeforeStateJson { get; set; }
    public string? AfterStateJson { get; set; }
    public string ActorId { get; set; } = "";
    public string Operation { get; set; } = "";
    public string SubjectId { get; set; } = "";
    public string Reason { get; set; } = "";
    public string? BeforeHash { get; set; }
    public string? AfterHash { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
