using Microsoft.EntityFrameworkCore;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;

namespace TaskMetricAI.Infrastructure.Persistence;

/// <summary>Operator-only installation registry, kept separate from tenant CRUD.</summary>
public sealed class InstallationDbContext(DbContextOptions<InstallationDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<TenantRegistration>().ToTable("TenantRegistration");
        model.Entity<TenantRegistration>().HasKey(x => x.TenantId);
        model.Entity<TenantRegistration>().HasAlternateKey(x => new { x.TenantId, x.OrganizationId });
        var row = model.Entity<TenantInstallation>();
        row.ToTable("TenantInstallation");
        row.HasKey(x => new { x.AppId, x.EnvironmentId, x.InstallationId, x.CloudId });
        row.HasIndex(x => new { x.TenantId, x.OrganizationId });
        row.HasOne<TenantRegistration>().WithMany().HasForeignKey(x => new { x.TenantId, x.OrganizationId })
            .HasPrincipalKey(x => new { x.TenantId, x.OrganizationId }).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Installation_TenantRegistration");
    }
}

public sealed class InstallationDirectory(InstallationDbContext db) : IInstallationDirectory
{
    public async Task<TenantIdentity?> ResolveAsync(VerifiedForgeIdentity identity, CancellationToken cancellationToken = default)
    {
        Require.That(new[] { identity.ActorId, identity.AppId, identity.EnvironmentId, identity.InstallationId, identity.CloudId }
            .All(x => !string.IsNullOrWhiteSpace(x)), 401, "invalid_verified_identity");
        var row = await db.Set<TenantInstallation>().AsNoTracking().SingleOrDefaultAsync(x => x.Enabled &&
            x.AppId == identity.AppId && x.EnvironmentId == identity.EnvironmentId &&
            x.InstallationId == identity.InstallationId && x.CloudId == identity.CloudId, cancellationToken);
        return row is null ? null : new TenantIdentity(row.TenantId, row.OrganizationId, identity.ActorId,
            row.CloudId, row.InstallationId, row.AppId, row.EnvironmentId);
    }
}
