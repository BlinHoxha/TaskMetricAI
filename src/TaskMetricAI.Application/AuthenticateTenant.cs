using TaskMetricAI.Domain;

namespace TaskMetricAI.Application;

/// <summary>Only this assembly's verified authentication path can issue a database-session capability.</summary>
public sealed class AuthenticatedTenant
{
    public TenantIdentity Identity { get; }
    internal AuthenticatedTenant(TenantIdentity identity) => Identity = identity;
}

/// <summary>The tenant is selected only after token validation and an exact operator-provisioned binding.</summary>
public sealed class AuthenticateTenant(IForgeIdentityVerifier verifier, IInstallationDirectory directory)
{
    public async Task<AuthenticatedTenant> AuthenticateAsync(string token, CancellationToken ct = default) =>
        new(await ExecuteAsync(token, ct));
    public async Task<TenantIdentity> ExecuteAsync(string token, CancellationToken ct = default)
    {
        var verified = await verifier.VerifyAsync(token, ct);
        var identity = await directory.ResolveAsync(verified, ct);
        Require.That(identity is not null, 401, "installation_not_registered");
        Require.That(identity!.ActorId == verified.ActorId && identity.AppId == verified.AppId &&
            identity.EnvironmentId == verified.EnvironmentId && identity.InstallationId == verified.InstallationId &&
            identity.CloudId == verified.CloudId, 401, "installation_identity_mismatch");
        return identity;
    }
}
