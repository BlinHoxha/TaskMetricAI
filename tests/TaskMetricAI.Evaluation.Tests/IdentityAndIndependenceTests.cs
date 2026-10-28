using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Evaluation.Tests;

public sealed class IdentityAndIndependenceTests
{
    [Fact]
    public void Canonical_content_has_fixed_schema_order_and_normalized_decimals()
    {
        var p = new EvaluationProfile("tenant", "p", 1, "N", "D", new(ScopeType.Team, "t"),
            [new("c", "C", "D", 1.0000m, new("a", "b", "c", "d", "e"))],
            [new("all", "All", new(1, 5, true, true))], Profiles.Governance);
        const string expected = """{"schema":"profile-content-v2","tenantId":"tenant","profileId":"p","version":1,"name":"N","description":"D","scopeType":"Team","scopeId":"t","percentageDecimalPlaces":2,"scoringPolicy":"wsm-v1","sensitivityPolicy":"relative-10-v1","criteria":[{"id":"c","label":"C","definition":"D","weight":"1","isEnabled":true,"sourceMetric":null,"anchors":["a","b","c","d","e"]}],"complexityBands":[{"id":"all","name":"All","lower":"1","upper":"5","includeLower":true,"includeUpper":true}],"storyPointBands":null}""";
        Assert.Equal(expected, p.CanonicalContent);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expected))).ToLowerInvariant(), p.ContentHash);
        Assert.Equal(64, p.ContentHash.Length);
    }

    [Fact]
    public void Content_hash_is_culture_scale_and_lifecycle_independent()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUi = CultureInfo.CurrentUICulture;
        try
        {
            string? expected = null;
            string? expectedEvaluation = null;
            foreach (var name in new[] { "en-US", "hu-HU", "pl-PL", "tr-TR" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
                var p = Profiles.Create(weights: [.50m, .50m], status: ProfileStatus.Active);
                expected ??= p.ContentHash;
                Assert.Equal(expected, p.ContentHash);
                var evaluation = JsonSerializer.Serialize(SensitivityAnalyzer.Analyze(p, Profiles.Scores(p, 1, 4)));
                expectedEvaluation ??= evaluation;
                Assert.Equal(expectedEvaluation, evaluation);
                var archive = Profiles.Create(weights: [.5m, .5m], status: ProfileStatus.Archived,
                    governance: Profiles.Governance with { CreatedBy = "other", ChangeReason = "archive", ApprovedBy = "approver", ApprovedAt = DateTimeOffset.UtcNow });
                Assert.Equal(expected, archive.ContentHash);
            }
        }
        finally { CultureInfo.CurrentCulture = originalCulture; CultureInfo.CurrentUICulture = originalUi; }
    }

    [Fact]
    public void Every_policy_content_change_changes_identity()
    {
        var p = Profiles.Create(weights: [.5m, .5m]);
        var criteria = p.Criteria.ToArray();
        var band = Profiles.Bands;
        var points = Profiles.Points;
        EvaluationProfile[] variants =
        [
            Profiles.Create(weights: [.4m, .6m]), Profiles.Create(weights: [.5m, .5m], id: "other"),
            Profiles.Create(weights: [.5m, .5m], version: 2), Profiles.Create(weights: [.5m, .5m], scope: new(ScopeType.Organization, "org")),
            Profiles.Create(weights: [.5m, .5m], points: false), Profiles.Create(weights: [.5m, .5m], policy: new(3)),
            Profiles.Create(weights: [.5m, .5m], name: "renamed"), Profiles.Create(weights: [.5m, .5m], description: "new definition"),
            Profiles.Create(criteria: [criteria[0] with { Anchors = criteria[0].Anchors with { Five = "new anchor" } }, criteria[1]]),
            Profiles.Create(criteria: criteria.Reverse().ToArray()),
            Profiles.Create(weights: [.5m, .5m], bands: band.Select((b, i) => i == 0 ? b with { Name = "new name" } : b).ToArray()),
            Profiles.Create(weights: [.5m, .5m], pointBands: points.Select((b, i) => i == 5 ? b with { RecommendSplit = false } : b).ToArray())
        ];
        Assert.All(variants, variant => Assert.NotEqual(p.ContentHash, variant.ContentHash));
    }

    [Fact]
    public void Caller_collections_cannot_mutate_snapshots_or_results()
    {
        var criteria = new[] { Profiles.Criterion("a", .5m), Profiles.Criterion("b", .5m) };
        var bands = Profiles.Bands;
        var points = Profiles.Points;
        var p = Profiles.Create(criteria: criteria, bands: bands, pointBands: points);
        var hash = p.ContentHash;
        criteria[0] = criteria[0] with { Weight = 0m };
        bands[0] = bands[0] with { Name = "mutated" };
        points[0] = points[0] with { Points = 99 };
        Assert.Equal(.5m, p.Criteria[0].Weight);
        Assert.Equal("Very Low", p.ComplexityBands[0].Name);
        Assert.Equal(1, p.StoryPointBands!.Value[0].Points);
        var scores = Profiles.Scores(p, 1, 5);
        var result = McdaEvaluator.Evaluate(p, scores);
        scores[0] = scores[0] with { Score = 5 };
        Assert.Equal(3m, result.WeightedScore);
        Assert.Equal(1, result.Contributions[0].Score);
        Assert.Equal(hash, p.ContentHash);
        var boxed = (IList)result.Contributions;
        Assert.Throws<NotSupportedException>(() => boxed[0] = result.Contributions[1]);
        Assert.All(typeof(EvaluationProfile).GetProperties(), property => Assert.Null(property.SetMethod));
    }

    [Fact]
    public async Task Concurrent_evaluations_and_hashes_are_deterministic()
    {
        var p = Profiles.Create();
        var scores = Profiles.Scores(p, 4, 3, 4, 3, 5, 3, 5, 2);
        var baseline = JsonSerializer.Serialize(SensitivityAnalyzer.Analyze(p, scores));
        await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(() =>
        {
            Assert.Equal(baseline, JsonSerializer.Serialize(SensitivityAnalyzer.Analyze(p, scores.Reverse())));
            Assert.Equal(p.ContentHash, Profiles.Create().ContentHash);
        })));
    }

    [Fact]
    public void Evaluation_assembly_has_only_BCL_references_and_no_packages_or_project_dependencies()
    {
        var assembly = typeof(McdaEvaluator).Assembly;
        Assert.All(assembly.GetReferencedAssemblies(), reference => Assert.StartsWith("System.", reference.Name));
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TaskMetricAI.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var project = XDocument.Load(Path.Combine(directory.FullName, "src", "TaskMetricAI.Evaluation", "TaskMetricAI.Evaluation.csproj"));
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("ProjectReference"));
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), r => r.Name!.Contains("Http", StringComparison.OrdinalIgnoreCase));
    }
}
