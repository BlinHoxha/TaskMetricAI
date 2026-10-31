using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;
using TaskMetricAI.Infrastructure.Persistence;

namespace TaskMetricAI.Backend.Tests;

public sealed partial class HttpSecurityTests
{
    [Fact]
    public async Task Project_resolution_uses_assigned_version_and_precedence_without_broader_read_authority()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await using (var setup = await fixture.Session(fixture.A))
        {
            await fixture.Service(setup).GrantAsync("project-reader", AccessRole.Developer, new(ScopeType.JiraProject, "project"), "project access");
            await fixture.Service(setup).TransitionAsync(fixture.ProfileA, 1, ProfileStatus.Validated, 0, "validate");
            await fixture.Service(setup).TransitionAsync(fixture.ProfileA, 1, ProfileStatus.Active, 1, "activate");
            await fixture.Service(setup).AssignAsync(new(ScopeType.Organization, "org"), fixture.ProfileA, 1, "default");
            await setup.CommitAsync();
        }
        await using var factory = new ApiFactory(fixture); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", fixture.Token(fixture.A, "project-reader"));
        async Task AssertSelected(string expected, ScopeType scope)
        {
            using var response = await client.GetAsync("/api/projects/project/effective-profile?profileId=untrusted&teamId=untrusted");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal((int)scope, result.GetProperty("selectedScope").GetProperty("type").GetInt32());
            var profile = result.GetProperty("profile");
            Assert.Equal(expected, profile.GetProperty("profileId").GetString());
            Assert.Equal(fixture.A, profile.GetProperty("tenantId").GetString());
            Assert.Equal(1, profile.GetProperty("version").GetInt32());
            Assert.Equal(64, profile.GetProperty("contentHash").GetString()!.Length);
        }
        await AssertSelected(fixture.ProfileA, ScopeType.Organization);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/profiles/{fixture.ProfileA}/versions/1")).StatusCode);
        await ActivateAssigned(fixture, "team-profile", new(ScopeType.Team, "team"));
        await AssertSelected("team-profile", ScopeType.Team);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/profiles/team-profile/versions/1")).StatusCode);
        await ActivateAssigned(fixture, "project-profile", new(ScopeType.JiraProject, "project"));
        await using (var newer = await fixture.Session(fixture.A))
        {
            await fixture.Service(newer).AddProfileVersionAsync("project-profile", PersistenceTests.Input(scope: new(ScopeType.JiraProject, "project")), 1);
            await newer.CommitAsync();
        }
        await AssertSelected("project-profile", ScopeType.JiraProject);
        await using (var archive = await fixture.Session(fixture.A))
        {
            await fixture.Service(archive).TransitionAsync("project-profile", 1, ProfileStatus.Archived, 2, "retire explicit override");
            await archive.CommitAsync();
        }
        await AssertSelected("team-profile", ScopeType.Team);
        string readerGrant;
        await using (var read = await fixture.Session(fixture.A))
            readerGrant = (await read.Db.Set<ScopeGrant>().SingleAsync(x => x.ActorId == "project-reader")).Id;
        await using (var revoke = await fixture.Session(fixture.A))
        { await fixture.Service(revoke).RevokeGrantAsync(readerGrant, "remove project entitlement"); await revoke.CommitAsync(); }
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/projects/project/effective-profile")).StatusCode);
    }

    [Fact]
    public async Task Missing_unregistered_and_foreign_scopes_fail_closed_even_for_administrators()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await Register(fixture, fixture.B, ["foreign-project"], [], []);
        await using var factory = new ApiFactory(fixture); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", fixture.Token(fixture.A));
        foreach (var project in new[] { "missing", "foreign-project", "project" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/projects/{project}/effective-profile")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/profiles/invalid/versions",
            new NewProfile(PersistenceTests.Input(scope: new(ScopeType.Team, "unregistered")), 0))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/scope-grants",
            new Grant("reader", AccessRole.Developer, new(ScopeType.JiraProject, "foreign-project"), "deny foreign"))).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", fixture.Token(fixture.A, "ungranted-actor"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/projects/project/effective-profile")).StatusCode);
    }

    [Fact]
    public async Task Ambiguous_team_assignments_never_fall_back_to_organization_or_pick_a_team()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await Register(fixture, fixture.A, [], ["second-team"], [("project", "second-team")]);
        await ActivateAssigned(fixture, "first-team-profile", new(ScopeType.Team, "team"));
        await ActivateAssigned(fixture, "second-team-profile", new(ScopeType.Team, "second-team"));
        await using var factory = new ApiFactory(fixture); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", fixture.Token(fixture.A));
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync("/api/projects/project/effective-profile")).StatusCode);
    }

    [Fact]
    public async Task Revocation_invalidates_same_token_and_already_loaded_grants_on_later_authorization()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await using var retained = await fixture.Session(fixture.A, "manager");
        var access = new AccessControl(new TenantRepository(retained.Db), retained.Identity);
        await access.RequireScopeAsync(new(ScopeType.Team, "team"), true);
        string grantId;
        await using (var read = await fixture.Session(fixture.A))
            grantId = (await read.Db.Set<ScopeGrant>().SingleAsync(x => x.ActorId == "manager")).Id;
        await using var factory = new ApiFactory(fixture); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", fixture.Token(fixture.A));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/scope-grants/{grantId}/revoke", new RevokeGrant("remove manager access"))).StatusCode);
        await Assert.ThrowsAsync<OperationException>(() => access.RequireScopeAsync(new(ScopeType.Team, "team"), true));
        client.DefaultRequestHeaders.Authorization = new("Bearer", fixture.Token(fixture.A, "manager"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/profiles/revoked/versions",
            new NewProfile(PersistenceTests.Input(scope: new(ScopeType.Team, "team")), 0))).StatusCode);
        await using var verify = await fixture.Session(fixture.A);
        var audit = await verify.Db.Set<AuditEvent>().SingleAsync(x => x.Operation == "scope.revoked");
        Assert.Equal("admin", audit.ActorId); Assert.Equal("manager", audit.TargetActorId);
        Assert.Contains("false", audit.AfterStateJson!);
    }

    [Fact]
    public async Task Revocation_rejects_foreign_grants_and_final_admin_and_serializes_concurrent_self_revocations()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        string adminId, foreignId;
        await using (var a = await fixture.Session(fixture.A)) adminId = (await a.Db.Set<ScopeGrant>().SingleAsync(x => x.ActorId == "admin")).Id;
        await using (var b = await fixture.Session(fixture.B)) foreignId = (await b.Db.Set<ScopeGrant>().SingleAsync(x => x.ActorId == "manager")).Id;
        await using var factory = new ApiFactory(fixture); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", fixture.Token(fixture.A));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/scope-grants/{foreignId}/revoke", new RevokeGrant("foreign"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/scope-grants/{adminId}/revoke", new RevokeGrant("last administrator"))).StatusCode);
        await using (var setup = await fixture.Session(fixture.A))
        {
            await fixture.Service(setup).GrantAsync("backup", AccessRole.SystemAdministrator, new(ScopeType.Organization, "org"), "backup administrator");
            await setup.CommitAsync();
        }
        var first = await fixture.Session(fixture.A, "admin");
        await using var second = await fixture.Session(fixture.A, "backup");
        var backupId = (await second.Db.Set<ScopeGrant>().SingleAsync(x => x.ActorId == "backup")).Id;
        // First revocation holds the real tenant advisory lock until commit.
        await fixture.Service(first).RevokeGrantAsync(adminId, "self revoke");
        var waiting = fixture.Service(second).RevokeGrantAsync(backupId, "competing self revoke");
        try
        {
            await WaitForAuthorizationLock(fixture.Runtime, ((NpgsqlConnection)second.Db.Database.GetDbConnection()).ProcessID);
            Assert.False(waiting.IsCompleted);
            await first.CommitAsync();
            var rejected = await Assert.ThrowsAsync<OperationException>(() => waiting);
            Assert.Equal(409, rejected.Status); Assert.Equal("final_administrator_required", rejected.Code);
        }
        finally
        {
            // Unblock and observe the competing task even if the lock assertion fails.
            await first.DisposeAsync();
            try { await waiting; } catch (OperationException) { }
        }
        // Operator verification does not depend on which administrator retained access.
        await using var verify = fixture.Operator(fixture.A);
        Assert.Single(await verify.Set<ScopeGrant>().Where(x => x.Role == AccessRole.SystemAdministrator).ToArrayAsync());
    }

    private static async Task WaitForAuthorizationLock(string runtime, int pid)
    {
        await using var connection = new NpgsqlConnection(runtime); await connection.OpenAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!deadline.IsCancellationRequested)
        {
            await using var probe = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_locks WHERE pid=@pid AND locktype='advisory' AND NOT granted)", connection);
            probe.Parameters.AddWithValue("pid", pid);
            if (await probe.ExecuteScalarAsync(deadline.Token) is true) return;
            await Task.Delay(20, deadline.Token);
        }
        Assert.Fail("Competing revocation never reached the tenant advisory lock.");
    }

    [Fact]
    public async Task Scope_registrations_and_mappings_are_read_only_to_runtime_and_tenant_foreign_keys_are_enforced()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await Register(fixture, fixture.B, ["b-only"], [], []);
        foreach (var sql in new[] { "INSERT INTO \"ProjectRegistration\" (\"TenantId\",\"Id\") VALUES ('forged','project')",
                     "DELETE FROM \"TeamRegistration\" WHERE false", "INSERT INTO \"ProjectTeam\" SELECT * FROM \"ProjectTeam\" WHERE false" })
        {
            await using var connection = new NpgsqlConnection(fixture.Runtime); await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            var denied = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }
        await using var db = fixture.Operator(fixture.A);
        db.Add(new ProjectTeam { TenantId = fixture.A, ProjectId = "b-only", TeamId = "team" });
        var foreign = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ((PostgresException)foreign.InnerException!).SqlState);
    }

    private static async Task ActivateAssigned(SecurityFixture fixture, string id, ProfileScope scope)
    {
        await using var session = await fixture.Session(fixture.A);
        var service = fixture.Service(session);
        await service.AddProfileVersionAsync(id, PersistenceTests.Input(scope: scope), 0);
        await service.TransitionAsync(id, 1, ProfileStatus.Validated, 0, "validate");
        await service.TransitionAsync(id, 1, ProfileStatus.Active, 1, "activate");
        await service.AssignAsync(scope, id, 1, "assign"); await session.CommitAsync();
    }

    [Fact]
    public async Task Concurrent_identical_project_ids_resolve_only_the_bound_tenants_profile()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        foreach (var tenant in new[] { fixture.A, fixture.B })
        {
            await using var session = await fixture.Session(tenant);
            var service = fixture.Service(session);
            await service.AddProfileVersionAsync("same-profile", PersistenceTests.Input(scope: new(ScopeType.JiraProject, "project")), 0);
            await service.TransitionAsync("same-profile", 1, ProfileStatus.Validated, 0, "validate");
            await service.TransitionAsync("same-profile", 1, ProfileStatus.Active, 1, "activate");
            await service.AssignAsync(new(ScopeType.JiraProject, "project"), "same-profile", 1, "assign");
            await session.CommitAsync();
        }
        await using var factory = new ApiFactory(fixture); using var client = factory.CreateClient();
        var tokens = new[] { fixture.Token(fixture.A), fixture.Token(fixture.B) };
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/projects/project/effective-profile");
            request.Headers.Authorization = new("Bearer", tokens[index % 2]);
            request.Headers.Add("X-Tenant-Id", index % 2 == 0 ? fixture.B : fixture.A);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(index % 2 == 0 ? fixture.A : fixture.B, result.GetProperty("profile").GetProperty("tenantId").GetString());
        }));
    }

    private static Task Register(SecurityFixture fixture, string tenant, string[] projects, string[] teams, (string Project, string Team)[] mappings) =>
        LocalDatabaseOperator.RegisterScopesAsync(fixture.Setup,
            new(tenant, "org", "admin", "11111111-1111-1111-1111-111111111111", tenant, fixture.AppId, "env"), projects, teams, mappings, "synthetic scopes");
}
