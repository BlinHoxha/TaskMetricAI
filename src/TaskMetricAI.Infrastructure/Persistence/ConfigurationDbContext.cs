using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Infrastructure.Persistence;

/// <summary>A fresh context per authenticated operation. Never pool across tenant identities.</summary>
public sealed class ConfigurationDbContext : DbContext
{
    public string TenantId { get; }
    private readonly bool operatorOnly;
    public ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options, TenantIdentity identity, bool operatorOnly = false) : base(options)
    { TenantId = identity.TenantId; this.operatorOnly = operatorOnly; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (operatorOnly) optionsBuilder.AddInterceptors(new TenantSessionInterceptor(TenantId));
        optionsBuilder.AddInterceptors(new TenantCommandGuard(TenantId, operatorOnly));
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        Entity<ScopeGrant>(model).HasIndex(x => new { x.TenantId, x.ActorId, x.ScopeType, x.ScopeId, x.Role }).IsUnique();
        Entity<MetricVersion>(model).HasIndex(x => new { x.TenantId, x.MetricId, x.Version }).IsUnique();
        Entity<CatalogVersion>(model).HasIndex(x => new { x.TenantId, x.CatalogId, x.Version }).IsUnique();
        var entries = Entity<CatalogEntry>(model);
        entries.HasIndex(x => new { x.TenantId, x.CatalogVersionId, x.MetricId }).IsUnique();
        entries.HasOne<CatalogVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.CatalogVersionId }).OnDelete(DeleteBehavior.Restrict);
        entries.HasOne<MetricVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.MetricVersionId }).OnDelete(DeleteBehavior.Restrict);
        var profiles = Entity<ProfileVersion>(model);
        profiles.HasIndex(x => new { x.TenantId, x.ProfileId, x.Version }).IsUnique();
        profiles.HasIndex(x => new { x.TenantId, x.ScopeType, x.ScopeId }).IsUnique().HasFilter("\"Status\" = 2");
        profiles.Property(x => x.Revision).IsConcurrencyToken();
        var assignments = Entity<ProfileAssignment>(model);
        assignments.Property(x => x.Revision).IsConcurrencyToken();
        assignments.HasIndex(x => new { x.TenantId, x.ScopeType, x.ScopeId }).IsUnique();
        assignments.HasOne<ProfileVersion>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProfileVersionId }).OnDelete(DeleteBehavior.Restrict);
        Entity<ProjectRegistration>(model);
        Entity<TeamRegistration>(model);
        var teams = Entity<ProjectTeam>(model);
        teams.HasIndex(x => new { x.TenantId, x.ProjectId, x.TeamId }).IsUnique();
        teams.HasOne<ProjectRegistration>().WithMany().HasForeignKey(x => new { x.TenantId, x.ProjectId }).OnDelete(DeleteBehavior.Restrict);
        teams.HasOne<TeamRegistration>().WithMany().HasForeignKey(x => new { x.TenantId, x.TeamId }).OnDelete(DeleteBehavior.Restrict);
        Entity<RepositoryConnection>(model).HasIndex(x => new { x.TenantId, x.RepositoryId }).IsUnique();
        var access = Entity<RepositoryAccess>(model);
        access.HasIndex(x => new { x.TenantId, x.ActorId, x.RepositoryConnectionId }).IsUnique();
        access.HasOne<RepositoryConnection>().WithMany().HasForeignKey(x => new { x.TenantId, x.RepositoryConnectionId }).OnDelete(DeleteBehavior.Restrict);
        Entity<RepositoryMapping>(model).HasOne<RepositoryConnection>().WithMany()
            .HasForeignKey(x => new { x.TenantId, x.RepositoryConnectionId }).OnDelete(DeleteBehavior.Restrict);
        var pinned = Entity<PinnedRepositorySnapshot>(model);
        pinned.HasOne<RepositoryMapping>().WithMany().HasForeignKey(x => new { x.TenantId, x.MappingId }).OnDelete(DeleteBehavior.Restrict);
        pinned.HasOne<RepositoryConnection>().WithMany().HasForeignKey(x => new { x.TenantId, x.RepositoryConnectionId }).OnDelete(DeleteBehavior.Restrict);
        Entity<AuditEvent>(model);
    }

    private EntityTypeBuilder<T> Entity<T>(ModelBuilder model) where T : TenantEntity
    {
        var entity = model.Entity<T>();
        entity.HasBaseType((Type?)null);
        entity.ToTable(typeof(T).Name);
        entity.HasKey(x => new { x.TenantId, x.Id });
        entity.Property(x => x.TenantId).HasMaxLength(200);
        entity.Property(x => x.Id).HasMaxLength(200);
        entity.HasQueryFilter(x => x.TenantId == TenantId);
        return entity;
    }

    private void GuardChanges()
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<TenantEntity>().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            Require.That(entry.Entity.TenantId == TenantId, 403, "cross_tenant_write");
            Require.Text(entry.Entity.Id, "entity_id_required");
            if (entry.State != EntityState.Added)
                Require.That(entry.OriginalValues.GetValue<string>(nameof(TenantEntity.TenantId)) == TenantId, 403, "cross_tenant_write");
            if (entry.Entity is MetricVersion or CatalogVersion or CatalogEntry or AuditEvent or PinnedRepositorySnapshot)
                Require.That(entry.State == EntityState.Added, 409, "immutable_record");
            if (entry.Entity is ProfileVersion row)
            {
                Require.That(entry.State != EntityState.Deleted, 409, "immutable_profile");
                if (entry.State == EntityState.Added)
                    Require.That(row.Status == ProfileStatus.Draft && row.Revision == 0, 422, "profile_must_start_draft");
                if (entry.State == EntityState.Modified)
                {
                    Require.That(entry.Properties.Where(p => p.IsModified).All(p => p.Metadata.Name is nameof(ProfileVersion.Status) or nameof(ProfileVersion.Revision)),
                        409, "immutable_profile_content");
                    var old = entry.OriginalValues.GetValue<ProfileStatus>(nameof(ProfileVersion.Status));
                    Require.That((old, row.Status) is (ProfileStatus.Draft, ProfileStatus.Validated) or
                        (ProfileStatus.Validated, ProfileStatus.Active) or (ProfileStatus.Active, ProfileStatus.Archived), 409, "invalid_profile_transition");
                    row.Revision = checked(entry.OriginalValues.GetValue<long>(nameof(ProfileVersion.Revision)) + 1);
                }
                _ = Documents.Restore(row);
            }
            if (entry.Entity is MetricVersion metric) _ = MetricDocuments.Restore(metric);
            if (entry.Entity is CatalogVersion catalog)
            {
                Require.Text(catalog.CatalogId, "catalog_id_required");
                Require.That(catalog.Version > 0, 422, "invalid_catalog_version");
                _ = new MetricCatalog(TenantId, Documents.Read<MetricDocument[]>(catalog.SnapshotJson).Select(x => x.Restore()));
            }
            if (entry.Entity is ProfileAssignment assignment && entry.State == EntityState.Modified)
                assignment.Revision = checked(entry.OriginalValues.GetValue<long>(nameof(ProfileAssignment.Revision)) + 1);
            if (entry.Entity is ScopeGrant grant)
                Require.That(Enum.IsDefined(grant.Role) && Enum.IsDefined(grant.ScopeType) &&
                    !string.IsNullOrWhiteSpace(grant.ScopeId) && !string.IsNullOrWhiteSpace(grant.ActorId), 422, "invalid_scope_grant");
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    { GuardChanges(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    { GuardChanges(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
}
