using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;

namespace TaskMetricAI.Infrastructure.Authentication;

public sealed record ForgeOptions(string AppId, string[] AllowedEnvironments);
public interface IForgeSigningKeys
{
    Task<IReadOnlyCollection<SecurityKey>> GetAsync(bool refresh, CancellationToken ct);
}

/// <summary>Fixed Atlassian signing-key authority; no token-controlled URL or isolated-cloud inference.</summary>
public sealed class AtlassianSigningKeys(HttpClient http, TimeProvider clock) : IForgeSigningKeys
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IReadOnlyCollection<SecurityKey> keys = [];
    private DateTimeOffset expires;

    public async Task<IReadOnlyCollection<SecurityKey>> GetAsync(bool refresh, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (!refresh && clock.GetUtcNow() < expires && keys.Count > 0) return keys;
            using var request = new HttpRequestMessage(HttpMethod.Get,
                "https://forge.cdn.prod.atlassian-dev.net/.well-known/jwks.json");
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            var body = await ProviderHttp.ReadJsonAsync(response, 65536, ct);
            using (body) keys = new JsonWebKeySet(body.RootElement.GetRawText()).GetSigningKeys().ToArray();
            Require.That(keys.Count > 0, 503, "forge_signing_keys_unavailable");
            expires = clock.GetUtcNow().AddMinutes(5);
            return keys;
        }
        finally { gate.Release(); }
    }
}

public sealed class ForgeIdentityVerifier(ForgeOptions options, IForgeSigningKeys signingKeys) : IForgeIdentityVerifier
{
    public async Task<VerifiedForgeIdentity> VerifyAsync(string token, CancellationToken cancellationToken = default)
    {
        Require.That(!string.IsNullOrWhiteSpace(options.AppId) && options.AllowedEnvironments.Length > 0,
            503, "forge_not_configured");
        Require.That(!string.IsNullOrWhiteSpace(token) && token.Length <= 16384, 401, "invalid_forge_token");
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false, MaximumTokenSizeInBytes = 16384 };
        SecurityToken validated;
        try
        {
            var keys = await signingKeys.GetAsync(false, cancellationToken);
            try { handler.ValidateToken(token, Parameters(keys), out validated); }
            catch (SecurityTokenSignatureKeyNotFoundException)
            {
                keys = await signingKeys.GetAsync(true, cancellationToken);
                handler.ValidateToken(token, Parameters(keys), out validated);
            }
            var jwt = (JwtSecurityToken)validated;
            using var json = JsonDocument.Parse(Base64UrlEncoder.Decode(jwt.RawPayload));
            var payload = json.RootElement;
            Require.That(!payload.TryGetProperty("icLabel", out _), 401, "unsupported_forge_cloud");
            var app = payload.GetProperty("app");
            var environment = app.GetProperty("environment");
            var appId = app.GetProperty("id").GetString()!;
            var environmentId = environment.GetProperty("id").GetString()!;
            var environmentType = environment.GetProperty("type").GetString()!;
            var installation = app.TryGetProperty("installation", out var installationObject)
                ? installationObject.GetProperty("id").GetString()! : app.GetProperty("installationId").GetString()!;
            if (app.TryGetProperty("installationId", out var oldInstallation))
                Require.That(oldInstallation.GetString() == installation, 401, "invalid_forge_token");
            var cloudId = payload.GetProperty("context").GetProperty("cloudId").GetString()!;
            var actor = payload.GetProperty("principal").GetString()!;
            Require.That(appId == options.AppId && options.AllowedEnvironments.Contains(environmentType, StringComparer.Ordinal),
                401, "invalid_forge_token");
            Require.That(Guid.TryParse(cloudId, out _) && !string.IsNullOrWhiteSpace(environmentId) &&
                !string.IsNullOrWhiteSpace(installation) && !string.IsNullOrWhiteSpace(actor), 401, "invalid_forge_token");
            if (installationObject.ValueKind == JsonValueKind.Object && installationObject.TryGetProperty("contexts", out var contexts))
                Require.That(contexts.EnumerateArray().Any(c => c.TryGetProperty("cloudId", out var cloud) && cloud.GetString() == cloudId),
                    401, "invalid_forge_token");
            return new(appId, environmentId, environmentType, installation, cloudId, actor);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or JsonException or
                                   KeyNotFoundException or InvalidOperationException)
        { throw new OperationException(401, "invalid_forge_token"); }
    }

    private TokenValidationParameters Parameters(IReadOnlyCollection<SecurityKey> keys) => new()
    {
        ValidateIssuer = true, ValidIssuer = "forge/invocation-token",
        ValidateAudience = true, ValidAudience = options.AppId,
        ValidateIssuerSigningKey = true, IssuerSigningKeys = keys,
        ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
        ClockSkew = TimeSpan.FromSeconds(30), ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
    };
}
