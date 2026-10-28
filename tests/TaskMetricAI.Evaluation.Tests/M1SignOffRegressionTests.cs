using System.Text.Json;
using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Evaluation.Tests;

public sealed class M1SignOffRegressionTests
{
    [Fact]
    public void Weight_only_revision_changes_estimate_without_rewriting_historical_evaluation()
    {
        var original = Profiles.Create(weights: [.8m, .2m], status: ProfileStatus.Active);
        var scores = Profiles.Scores(original, 1, 5);
        var old = new EvaluationSnapshot(original.TenantId, "original", original, original.Reference, scores);
        var oldSerialized = JsonSerializer.Serialize(old);
        var revised = original.CreateNextVersion(
            [original.Criteria[0] with { Weight = .2m }, original.Criteria[1] with { Weight = .8m }],
            original.ComplexityBands, original.StoryPointBands,
            Profiles.Governance with { ChangeReason = "Change importance only" });
        var current = new EvaluationSnapshot(revised.TenantId, "revised", revised, revised.Reference, scores);

        Assert.Equal(1.8m, old.Result.WeightedScore);
        Assert.Equal(4.2m, current.Result.WeightedScore);
        Assert.Equal(2, old.Result.PointSuggestion!.Points);
        Assert.Equal(13, current.Result.PointSuggestion!.Points);
        Assert.Equal(1, old.Profile.Version);
        Assert.Equal(2, current.Profile.Version);
        Assert.NotEqual(old.Profile.ContentHash, current.Profile.ContentHash);
        Assert.Equal(oldSerialized, JsonSerializer.Serialize(old));
        Assert.Equal(old.Result.WeightedScore, McdaEvaluator.Evaluate(old.Profile, old.Profile.Reference, old.Scores).WeightedScore);
        Assert.Throws<EvaluationValidationException>(() => McdaEvaluator.Evaluate(revised, old.Profile.Reference, old.Scores));
    }

    [Fact]
    public void Revised_catalog_rubric_is_copied_into_a_new_profile_while_old_snapshot_stays_exact()
    {
        var metric = new MetricDefinition("tenant", "custom", 1, MetricKind.Custom, "Specialist review",
            "Review scope", new("One reviewer", "Two reviewers", "Several reviewers", "Cross-team review", "External review"));
        var profile = Profiles.Create(tenantId: metric.TenantId, criteria: [CriterionDefinition.FromMetric(metric, 1)]);
        var old = new EvaluationSnapshot(profile.TenantId, "historical", profile, profile.Reference, Profiles.Scores(profile, 3));
        var serialized = JsonSerializer.Serialize(old);
        var metricV2 = metric.CreateNextVersion("Updated review metric", "Updated scope",
            metric.Anchors with { Three = "Three independent domain reviewers" });
        var catalogV2 = new MetricCatalog(metric.TenantId, [metricV2]);
        var profileV2 = profile.CreateNextVersion([CriterionDefinition.FromMetric(catalogV2.GetRequired("custom"), 1)],
            profile.ComplexityBands, profile.StoryPointBands, Profiles.Governance with { ChangeReason = "Adopt rubric v2" });

        Assert.Equal(2, profileV2.Criteria[0].SourceMetric!.Version);
        Assert.Equal("Three independent domain reviewers", profileV2.Criteria[0].Anchors.Three);
        Assert.Equal(1, old.Profile.Criteria[0].SourceMetric!.Version);
        Assert.Equal("Several reviewers", old.Profile.Criteria[0].Anchors.Three);
        Assert.NotEqual(old.Profile.ContentHash, profileV2.ContentHash);
        Assert.Equal(serialized, JsonSerializer.Serialize(old));
    }

    [Fact]
    public void Pinned_v2_hash_is_stable_and_a_legacy_v1_hash_is_not_silently_accepted()
    {
        var p = new EvaluationProfile("tenant", "p", 1, "N", "D", new(ScopeType.Team, "t"),
            [new("c", "C", "D", 1m, new("a", "b", "c", "d", "e"))],
            [new("all", "All", new(1, 5, true, true))], Profiles.Governance);
        // Golden digests of the fixed JSON fixtures, calculated independently of the production canonicalizer.
        const string v2 = "65b693a6848225c42c9f940bbb44dca25fa2458f4b679876c9b4dc3dd97564a4";
        const string v1 = "d496bd874213b8afe8f61a7312270eded87805aefbef0235e601c096e59c0614";
        Assert.Equal(v2, p.ContentHash);
        Assert.Equal(3m, McdaEvaluator.Evaluate(p, new("tenant", "p", 1, v2), [new("c", 3)]).WeightedScore);
        var error = Assert.Throws<EvaluationValidationException>(() =>
            McdaEvaluator.Evaluate(p, new("tenant", "p", 1, v1), [new("c", 3)]));
        Assert.Contains(error.Errors, e => e.Code == "assessment.profile");
        Assert.Equal(v2, p.ContentHash);
        // Rejection is intentional; this does not claim a legacy-v1 deserializer exists.
    }

    [Fact]
    public void Definition_and_profile_versions_cannot_wrap_at_integer_limit()
    {
        var p = Profiles.Create(weights: [1], version: int.MaxValue);
        Assert.Throws<OverflowException>(() => p.CreateNextVersion(p.Criteria, p.ComplexityBands, p.StoryPointBands,
            Profiles.Governance with { ChangeReason = "Overflow attempt" }));
        var metric = new MetricDefinition("tenant", "custom", int.MaxValue, MetricKind.Custom, "Label", "Description",
            new("a", "b", "c", "d", "e"));
        Assert.Throws<OverflowException>(() => metric.CreateNextVersion(metric.Label, metric.Description, metric.Anchors));
        Assert.Equal(int.MaxValue, p.Version);
        Assert.Equal(int.MaxValue, metric.Version);
    }

    [Fact]
    public void Disabled_content_changes_identity_but_not_scoring_or_sensitivity_numbers()
    {
        var baseline = Profiles.Create(weights: [.3m, .7m]);
        var extraDisabled = Profiles.Create(criteria:
            [Profiles.Criterion("excluded", .9m) with { IsEnabled = false }, .. baseline.Criteria]);
        var a = SensitivityAnalyzer.Analyze(baseline, Profiles.Scores(baseline, 1, 5));
        var b = SensitivityAnalyzer.Analyze(extraDisabled, Profiles.Scores(extraDisabled, 1, 5));

        Assert.NotEqual(baseline.ContentHash, extraDisabled.ContentHash);
        Assert.Equal(3.8m, a.Baseline.WeightedScore);
        Assert.Equal(a.Baseline.WeightedScore, b.Baseline.WeightedScore);
        Assert.Equal(a.Scenarios.Length, b.Scenarios.Length);
        for (var i = 0; i < a.Scenarios.Length; i++)
        {
            Assert.Equal(a.Scenarios[i].Weights.ToArray(), b.Scenarios[i].Weights.ToArray());
            Assert.Equal(a.Scenarios[i].HypotheticalResult!.WeightedScore, b.Scenarios[i].HypotheticalResult!.WeightedScore);
            Assert.Equal(a.Scenarios[i].ClassificationChanged, b.Scenarios[i].ClassificationChanged);
            Assert.Equal(a.Scenarios[i].PointsChanged, b.Scenarios[i].PointsChanged);
        }
    }
}
