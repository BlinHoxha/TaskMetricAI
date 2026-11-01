using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using TaskMetricAI.Api;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;
using TaskMetricAI.Infrastructure.Authentication;
using TaskMetricAI.Infrastructure;
using TaskMetricAI.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
// This host can access only runtime credentials. It never initializes schema or registers installations.
builder.Services.AddSingleton(sp =>
{
    var reference = sp.GetRequiredService<IConfiguration>()["TASKMETRICAI_RUNTIME_CONNECTION_FILE"]
        ?? throw new InvalidOperationException("Runtime connection file must be configured outside Git.");
    var connection = File.ReadAllText(reference).Trim(); RuntimeDatabase.ValidateSettings(connection);
    return new RuntimeConnection(connection);
});
builder.Services.AddHostedService<RuntimeStartupGuard>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => new ForgeOptions(sp.GetRequiredService<IConfiguration>()["TASKMETRICAI_FORGE_APP_ID"] ?? "",
    (sp.GetRequiredService<IConfiguration>()["TASKMETRICAI_FORGE_ENVIRONMENTS"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
builder.Services.AddHttpClient("forge-jwks", http => http.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<IForgeSigningKeys>(sp => new AtlassianSigningKeys(sp.GetRequiredService<IHttpClientFactory>().CreateClient("forge-jwks"), TimeProvider.System));
builder.Services.AddScoped<IForgeIdentityVerifier, ForgeIdentityVerifier>();
builder.Services.AddScoped<IInstallationDirectory>(sp => new RuntimeInstallationDirectory(sp.GetRequiredService<RuntimeConnection>().Value));
builder.Services.AddScoped<AuthenticateTenant>();
builder.Services.AddHttpClient("jira", http => http.Timeout = TimeSpan.FromSeconds(15))
    .RemoveAllLoggers().RedactLoggedHeaders(_ => true)
    .ConfigurePrimaryHttpMessageHandler(JiraCloudReader.CreateTransport);
builder.Services.AddScoped<IJiraReader>(sp => new JiraCloudReader(sp.GetRequiredService<IHttpClientFactory>().CreateClient("jira")));
builder.Services.AddAuthentication("Forge").AddScheme<AuthenticationSchemeOptions, ForgeAuthenticationHandler>("Forge", _ => { });
builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.ConfigureHttpJsonOptions(options => ConfigurationJson.Configure(options.SerializerOptions));
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 262144);
var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    if (context.Request.ContentLength > 262144) { context.Response.StatusCode = 413; return; }
    var callerAborted = context.RequestAborted;
    using var jiraDeadline = context.GetEndpoint()?.Metadata.GetMetadata<JiraReadBoundary>() is not null
        ? CancellationTokenSource.CreateLinkedTokenSource(callerAborted) : null;
    if (jiraDeadline is not null)
    {
        jiraDeadline.CancelAfter(TimeSpan.FromSeconds(20));
        context.RequestAborted = jiraDeadline.Token;
    }
    try { await next(context); }
    catch (OperationException ex) when (!context.Response.HasStarted)
    {
        // Do not disclose whether a forbidden scoped resource exists.
        var status = ex.Status == 403 ? 404 : ex.Status;
        context.Response.StatusCode = status;
        if (status == 429 && ex.RetryAfterSeconds is >= 0 and <= 300)
            context.Response.Headers.RetryAfter = ex.RetryAfterSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await context.Response.WriteAsJsonAsync(new { error = status == 404 ? "resource_not_found" : ex.Code });
    }
    catch (EvaluationValidationException) when (!context.Response.HasStarted)
    { context.Response.StatusCode = 422; await context.Response.WriteAsJsonAsync(new { error = "invalid_profile_configuration" }); }
    catch (BadHttpRequestException ex) when (!context.Response.HasStarted)
    { context.Response.StatusCode = ex.StatusCode; await context.Response.WriteAsJsonAsync(new { error = "invalid_request" }); }
    catch (OperationCanceledException) when (callerAborted.IsCancellationRequested) { }
    catch (OperationCanceledException) when (jiraDeadline?.IsCancellationRequested == true && !context.Response.HasStarted)
    { context.Response.StatusCode = 504; await context.Response.WriteAsJsonAsync(new { error = "jira_timeout" }, callerAborted); }
    catch (Exception) when (!context.Response.HasStarted)
    { context.Response.StatusCode = 503; await context.Response.WriteAsJsonAsync(new { error = "operation_unavailable" }); }
    finally { context.RequestAborted = callerAborted; }
});
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/profiles/{id}/versions/{version:int}", (HttpContext context, string id, int version) =>
    Run(context, service => service.ReadProfileAsync(id, version, context.RequestAborted)));
app.MapGet("/api/projects/{projectId}/effective-profile", (HttpContext context, string projectId) =>
    Run(context, service => service.ResolveProfileAsync(projectId, context.RequestAborted)));
app.MapGet("/api/projects/{projectId}/jira/issues/{issueId}", async (HttpContext context, string projectId, string issueId) =>
{
    if (context.Items[ForgeAuthenticationHandler.BoundTenantKey] is not AuthenticatedTenant tenant) return Results.Unauthorized();
    Require.That(context.Request.Query.Count == 0, 422, "jira_query_denied");
    var header = context.Request.Headers["x-forge-oauth-user"];
    Require.That(header.Count == 1, 401, "jira_user_token_required");
    var credential = new JiraUserCredential(header.ToString());
    var runtime = context.RequestServices.GetRequiredService<RuntimeConnection>();
    await using var session = await TenantDatabaseSession.OpenAsync(runtime.Value, tenant, context.RequestAborted);
    var service = new JiraIssueService(new TenantRepository(session.Db), context.RequestServices.GetRequiredService<IJiraReader>());
    var issue = await service.ReadAsync(tenant, credential, projectId, issueId, context.RequestAborted);
    await session.CommitAsync(context.RequestAborted);
    return Results.Ok(issue);
}).WithMetadata(new JiraReadBoundary());
app.MapPost("/api/scope-grants/{id}/revoke", (HttpContext context, string id, RevokeGrant request) =>
    Run(context, async service => { await service.RevokeGrantAsync(id, request.Reason, context.RequestAborted); return new { changed = true }; }));
app.MapPost("/api/profiles/{id}/versions", (HttpContext context, string id, NewProfile request) =>
    Run(context, service => service.AddProfileVersionAsync(id, request.Input, request.ExpectedVersion, context.RequestAborted)));
app.MapPost("/api/profiles/{id}/versions/{version:int}/lifecycle", (HttpContext context, string id, int version, Lifecycle request) =>
    Run(context, async service => { await service.TransitionAsync(id, version, request.Status, request.ExpectedRevision, request.Reason, context.RequestAborted); return new { changed = true }; }));
app.MapPost("/api/profile-assignments", (HttpContext context, Assignment request) =>
    Run(context, async service => { await service.AssignAsync(request.Scope, request.ProfileId, request.Version, request.Reason, context.RequestAborted); return new { changed = true }; }));
app.MapGet("/api/catalogs/{id}/versions/{version:int}", (HttpContext context, string id, int version) =>
    Run(context, service => service.ReadCatalogAsync(id, version, context.RequestAborted)));
app.MapPost("/api/catalogs/{id}/versions", (HttpContext context, string id, NewCatalog request) =>
    Run(context, service => service.AddCatalogVersionAsync(id, request.Selection, request.ExpectedVersion, request.Reason, context.RequestAborted)));
app.MapPost("/api/metrics/{id}/versions", (HttpContext context, string id, NewMetric request) =>
    Run(context, service => service.AddMetricVersionAsync(id, request.Input, request.ExpectedVersion, context.RequestAborted)));
app.MapPost("/api/scope-grants", (HttpContext context, Grant request) =>
    Run(context, async service => { await service.GrantAsync(request.TargetActorId, request.Role, request.Scope, request.Reason, context.RequestAborted); return new { changed = true }; }));
app.Run();

static async Task<IResult> Run<T>(HttpContext context, Func<ConfigurationService, Task<T>> operation)
{
    if (context.Items[ForgeAuthenticationHandler.BoundTenantKey] is not AuthenticatedTenant tenant) return Results.Unauthorized();
    var runtime = context.RequestServices.GetRequiredService<RuntimeConnection>();
    await using var session = await TenantDatabaseSession.OpenAsync(runtime.Value, tenant, context.RequestAborted);
    var service = new ConfigurationService(new TenantRepository(session.Db), session.Identity, TimeProvider.System);
    var value = await operation(service);
    // Commit before an HTTP success response can be written. Failure/cancellation disposes and rolls back.
    await session.CommitAsync(context.RequestAborted);
    return Results.Ok(value);
}

public sealed record RuntimeConnection(string Value)
{
    public override string ToString() => "RuntimeConnection [redacted]";
}
public sealed record NewProfile(ProfileInput Input, int ExpectedVersion);
public sealed record NewMetric(MetricInput Input, int ExpectedVersion);
public sealed record NewCatalog(MetricReference[] Selection, int ExpectedVersion, string Reason);
public sealed record Lifecycle(ProfileStatus Status, long ExpectedRevision, string Reason);
public sealed record Assignment(ProfileScope Scope, string ProfileId, int Version, string Reason);
public sealed record Grant(string TargetActorId, AccessRole Role, ProfileScope Scope, string Reason);
public sealed record RevokeGrant(string Reason);
public sealed class JiraReadBoundary;
public partial class Program { }
