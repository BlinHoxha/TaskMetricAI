using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using TaskMetricAI.Application;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Backend.Tests;

public sealed partial class HttpSecurityTests
{
    public static IEnumerable<object[]> InvalidConfigurationBodies()
    {
        var profile = JsonSerializer.SerializeToNode(new NewProfile(PersistenceTests.Input(), 0), Documents.Json)!;
        var metric = JsonSerializer.SerializeToNode(new NewMetric(new("Synthetic metric", "Synthetic description",
            new("one", "two", "three", "four", "five"), "synthetic creation"), 0), Documents.Json)!;
        var grant = JsonSerializer.SerializeToNode(new Grant("target", AccessRole.Developer,
            new(ScopeType.Organization, "org"), "synthetic grant"), Documents.Json)!;
        foreach (var field in new[] { "input", "expectedVersion" })
        {
            var body = profile.DeepClone(); body.AsObject().Remove(field);
            yield return ["/api/profiles/rejected/versions", body.ToJsonString(), 400];
        }
        foreach (var field in new[] { "scope", "criteria", "complexityBands", "policy" })
        {
            var body = profile.DeepClone(); body["input"]![field] = null;
            yield return ["/api/profiles/rejected/versions", body.ToJsonString(), 400];
        }
        foreach (var field in new[] { "criteria", "complexityBands", "storyPointBands" })
        {
            var body = profile.DeepClone(); body["input"]![field] = new JsonArray((JsonNode?)null);
            yield return ["/api/profiles/rejected/versions", body.ToJsonString(), 422];
        }
        var nullInput = metric.DeepClone(); nullInput["input"] = null;
        yield return ["/api/metrics/rejected/versions", nullInput.ToJsonString(), 400];
        yield return ["/api/catalogs/rejected/versions", """{"selection":null,"expectedVersion":0,"reason":"synthetic"}""", 400];
        yield return ["/api/catalogs/rejected/versions", """{"selection":[null],"expectedVersion":0,"reason":"synthetic"}""", 422];
        foreach (var field in new[] { "role", "reason" })
        {
            var body = grant.DeepClone(); body.AsObject().Remove(field);
            yield return ["/api/scope-grants", body.ToJsonString(), 400];
        }
        yield return ["/api/profiles/{own}/versions/1/lifecycle", """{"status":1,"reason":"synthetic"}""", 400];
        yield return ["/api/profiles/{own}/versions/1/lifecycle", """{"expectedRevision":0,"reason":"synthetic"}""", 400];
        // An omitted nested scope discriminator must not silently mean Organization (enum zero).
        var noScopeType = profile.DeepClone(); noScopeType["input"]!["scope"]!.AsObject().Remove("type");
        yield return ["/api/profiles/rejected/versions", noScopeType.ToJsonString(), 400];
        yield return ["/api/scope-grants", """{"targetActorId":"target","role":0,"role":3,"scope":{"type":0,"id":"org"},"reason":"synthetic"}""", 400];
        yield return ["/api/scope-grants", """{"targetActorId":"target","role":0,"scope":{"type":0,"id":"org","id":"other"},"reason":"synthetic"}""", 400];
        yield return ["/api/profiles/{own}/versions/1/lifecycle", """{"status":1,"expectedRevision":5,"expectedRevision":0,"reason":"synthetic"}""", 400];
        yield return ["/api/scope-grants/absent/revoke", "{}", 400];
    }

    [Theory]
    [MemberData(nameof(InvalidConfigurationBodies))]
    public async Task Invalid_configuration_is_rejected_without_persisting_content_or_audit(string path, string json, int status)
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        int before;
        await using (var verify = await fixture.Session(fixture.A))
            before = await verify.Db.Set<AuditEvent>().CountAsync();
        await using var factory = new ApiFactory(fixture);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", fixture.Token(fixture.A));
        using var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync(path.Replace("{own}", fixture.ProfileA), body);
        Assert.Equal((HttpStatusCode)status, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        await using var after = await fixture.Session(fixture.A);
        Assert.Equal(before, await after.Db.Set<AuditEvent>().CountAsync());
        Assert.Single(await after.Db.Set<ProfileVersion>().ToArrayAsync());
        Assert.Empty(await after.Db.Set<MetricVersion>().ToArrayAsync());
        Assert.Empty(await after.Db.Set<CatalogVersion>().ToArrayAsync());
        Assert.False(await after.Db.Set<ScopeGrant>().AnyAsync(x => x.ActorId == "target"));
    }

    [Fact]
    public async Task Optional_point_mapping_remains_optional_and_anonymous_malformed_commands_are_unauthorized()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await using var factory = new ApiFactory(fixture); using var client = factory.CreateClient();
        using var malformed = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/scope-grants", malformed)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", fixture.Token(fixture.A));
        var body = JsonSerializer.SerializeToNode(new NewProfile(PersistenceTests.Input(), 0), Documents.Json)!;
        body["input"]!.AsObject().Remove("storyPointBands");
        var response = await client.PostAsJsonAsync("/api/profiles/optional-points/versions", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(fixture.A, result.GetProperty("tenantId").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("storyPointBands").ValueKind);
    }

    [Fact]
    public async Task Identical_resource_ids_remain_tenant_local_under_concurrent_http_requests()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        foreach (var tenant in new[] { fixture.A, fixture.B })
        {
            await using var session = await fixture.Session(tenant);
            var input = PersistenceTests.Input() with { Name = tenant };
            await fixture.Service(session).AddProfileVersionAsync("same-id", input, 0);
            await session.CommitAsync();
        }
        await using var factory = new ApiFactory(fixture); using var client = factory.CreateClient();
        var tokens = new[] { fixture.Token(fixture.A), fixture.Token(fixture.B) };
        var requests = Enumerable.Range(0, 12).Select(async index =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/profiles/same-id/versions/1");
            request.Headers.Authorization = new("Bearer", tokens[index % 2]);
            request.Headers.Add("X-Tenant-Id", index % 2 == 0 ? fixture.B : fixture.A);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(index % 2 == 0 ? fixture.A : fixture.B, result.GetProperty("tenantId").GetString());
            Assert.Equal(index % 2 == 0 ? fixture.A : fixture.B, result.GetProperty("name").GetString());
        });
        await Task.WhenAll(requests);
    }

    [Fact]
    public async Task Foreign_and_missing_identifiers_have_equivalent_denials_and_unsupported_listing_delete_stay_closed()
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await using var factory = new ApiFactory(fixture); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", fixture.Token(fixture.A));
        foreach (var action in new[] { "read", "lifecycle", "assignment" })
        {
            async Task<HttpResponseMessage> Attempt(string id) => action switch
            {
                "read" => await client.GetAsync($"/api/profiles/{id}/versions/1"),
                "lifecycle" => await client.PostAsJsonAsync($"/api/profiles/{id}/versions/1/lifecycle", new Lifecycle(ProfileStatus.Validated, 0, "synthetic denial")),
                _ => await client.PostAsJsonAsync("/api/profile-assignments", new Assignment(new(ScopeType.Organization, "org"), id, 1, "synthetic denial"))
            };
            using var foreign = await Attempt(fixture.ProfileB);
            using var missing = await Attempt("absent-" + Guid.NewGuid().ToString("N"));
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
            Assert.Equal(missing.StatusCode, foreign.StatusCode);
            Assert.Equal(await missing.Content.ReadAsStringAsync(), await foreign.Content.ReadAsStringAsync());
        }
        foreach (var path in new[] { "/api/profiles", "/api/metrics", "/api/catalogs", "/api/audit", "/api/evaluations", "/api/integrations" })
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        foreach (var id in new[] { fixture.ProfileA, fixture.ProfileB, "absent" })
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.DeleteAsync($"/api/profiles/{id}/versions/1")).StatusCode);
        await using var verify = await fixture.Session(fixture.B);
        Assert.Equal(ProfileStatus.Draft, (await fixture.Service(verify).ReadProfileAsync(fixture.ProfileB, 1)).Status);
    }

    [Theory]
    [InlineData("developer")]
    [InlineData("ungranted-actor")]
    public async Task Read_only_or_ungranted_actors_cannot_use_any_configuration_mutation_route(string actor)
    {
        await using var fixture = await SecurityFixture.CreateAsync();
        await using var factory = new ApiFactory(fixture); using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", fixture.Token(fixture.A, actor));
        int before;
        await using (var session = await fixture.Session(fixture.A))
            before = await session.Db.Set<AuditEvent>().CountAsync();
        var commands = new (string Path, object Body)[]
        {
            ("/api/profiles/forbidden/versions", new NewProfile(PersistenceTests.Input(), 0)),
            ($"/api/profiles/{fixture.ProfileA}/versions/1/lifecycle", new Lifecycle(ProfileStatus.Validated, 0, "denied")),
            ("/api/profile-assignments", new Assignment(new(ScopeType.Organization, "org"), fixture.ProfileA, 1, "denied")),
            ("/api/catalogs/forbidden/versions", new NewCatalog([], 0, "denied")),
            ("/api/metrics/forbidden/versions", new NewMetric(new("metric", "description", new("1", "2", "3", "4", "5"), "denied"), 0)),
            ("/api/scope-grants", new Grant(actor, AccessRole.SystemAdministrator, new(ScopeType.Organization, "org"), "denied")),
            ("/api/scope-grants/absent/revoke", new RevokeGrant("denied"))
        };
        foreach (var (path, body) in commands)
        {
            using var response = await client.PostAsJsonAsync(path, body);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        await using var verify = await fixture.Session(fixture.A);
        Assert.Equal(before, await verify.Db.Set<AuditEvent>().CountAsync());
        Assert.Single(await verify.Db.Set<ProfileVersion>().ToArrayAsync());
        Assert.Empty(await verify.Db.Set<ProfileAssignment>().ToArrayAsync());
        Assert.Empty(await verify.Db.Set<MetricVersion>().ToArrayAsync());
        Assert.Empty(await verify.Db.Set<CatalogVersion>().ToArrayAsync());
        Assert.Equal(ProfileStatus.Draft, (await fixture.Service(verify).ReadProfileAsync(fixture.ProfileA, 1)).Status);
    }
}
