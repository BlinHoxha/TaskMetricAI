using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;

namespace TaskMetricAI.Api;

public sealed class ForgeAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, AuthenticateTenant authenticate) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public static readonly object BoundTenantKey = new();
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization;
        if (header.Count != 1 || !header.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.Fail("forge_authentication_required");
        var token = header.ToString()[7..];
        try
        {
            var tenant = await authenticate.AuthenticateAsync(token, Context.RequestAborted);
            Context.Items[BoundTenantKey] = tenant;
            // Client role/tenant claims and headers are never copied into this principal.
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, tenant.Identity.ActorId)], Scheme.Name));
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }
        catch (OperationException ex) when (ex.Status is 401 or 403)
        { return AuthenticateResult.Fail("invalid_forge_identity"); }
    }
}
