using System.Collections;
using System.Text.Json;
using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Evaluation.Tests;

public sealed class MetricAgnosticSnapshotTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(20)]
    [InlineData(50)]
    public void Engine_accepts_arbitrary_metric_counts_without_standard_ids(int count)
    {
        var criteria = Enumerable.Range(0, count).Select(i => Profiles.Criterion($"custom-{i}", i == count - 1 ? 1m - (count - 1) * .01m : .01m)).ToArray();
        var p = Profiles.Create(criteria: criteria);
        var scores = Profiles.Scores(p, Enumerable.Repeat<int?>(3, count).ToArray());
        var r = McdaEvaluator.Evaluate(p, scores);
        Assert.Equal(3m, r.WeightedScore);
        Assert.Equal(count, r.Contributions.Length);
        Assert.Equal(criteria.Select(c => c.CriterionId), r.Contributions.Select(c => c.CriterionId));
    }

    [Fact]
    public void Disabled_metrics_are_excluded_but_enabled_zero_weight_unknowns_remain_visible()
    {
        var p = Profiles.Create(criteria:
        [
            Profiles.Criterion("disabled-first", .9m) with { IsEnabled = false },
            Profiles.Criterion("a", .4m), Profiles.Criterion("zero", 0),
            Profiles.Criterion("disabled-middle", .8m) with { IsEnabled = false }, Profiles.Criterion("b", .6m)
        ]);
        var scores = Profiles.Scores(p, 2, null, 4);
        var r = McdaEvaluator.Evaluate(p, scores);
        Assert.Equal(3.2m, r.WeightedScore);
        Assert.Equal(new[] { "a", "zero", "b" }, p.ActiveCriteria.Select(c => c.CriterionId));
        Assert.Equal(new[] { "a", "zero", "b" }, r.Contributions.Select(c => c.CriterionId));
        Assert.Equal("zero", Assert.Single(r.UnknownCriteria).CriterionId);
        Assert.False(r.RequiresRefinement);
        Assert.Throws<EvaluationValidationException>(() => McdaEvaluator.Evaluate(p, scores.Append(new("disabled-first", null))));
        Assert.Throws<EvaluationValidationException>(() => McdaEvaluator.Evaluate(p, scores.Where(s => s.CriterionId != "zero")));

        var sensitivity = SensitivityAnalyzer.Analyze(p, scores);
        Assert.Equal(6, sensitivity.Scenarios.Length);
        Assert.DoesNotContain(sensitivity.Scenarios, s => s.TargetCriterionId.StartsWith("disabled", StringComparison.Ordinal));
        foreach (var scenario in sensitivity.Scenarios.Where(s => s.Status == SensitivityStatus.Calculated))
        {
            Assert.Equal(new[] { "a", "zero", "b" }, scenario.Weights.Select(w => w.CriterionId));
            Assert.Equal(1m, scenario.Weights.Sum(w => w.Weight));
            Assert.Equal(0m, scenario.Weights.Single(w => w.CriterionId == "zero").Weight);
        }
    }

    [Fact]
    public void At_least_one_enabled_metric_and_exact_enabled_total_are_required()
    {
        Assert.Contains(Assert.Throws<EvaluationValidationException>(() => Profiles.Create(criteria:
            [Profiles.Criterion("off", 1) with { IsEnabled = false }])).Errors, e => e.Code == "criteria.active");
        Assert.Contains(Assert.Throws<EvaluationValidationException>(() => Profiles.Create(criteria:
            [Profiles.Criterion("a", .6m), Profiles.Criterion("off", .4m) with { IsEnabled = false }])).Errors,
            e => e.Code == "weights.total");
        var valid = Profiles.Create(criteria: [Profiles.Criterion("a", 1), Profiles.Criterion("off", 1) with { IsEnabled = false }]);
        Assert.Equal(1m, valid.ActiveCriteria.Sum(c => c.Weight));
        Assert.Equal(2m, valid.Criteria.Sum(c => c.Weight));
        Assert.Equal(1m, McdaEvaluator.Evaluate(valid, Profiles.Scores(valid, 1)).WeightedScore);
    }

    [Fact]
    public void Disabled_definitions_still_require_unique_ids_valid_rubrics_and_weight_ranges()
    {
        var active = Profiles.Criterion("a", 1);
        foreach (var disabled in new[]
                 {
                     active with { IsEnabled = false },
                     Profiles.Criterion("off", -1) with { IsEnabled = false },
                     Profiles.Criterion("off", 1.1m) with { IsEnabled = false },
                     Profiles.Criterion("off", .12345m) with { IsEnabled = false },
                     Profiles.Criterion("off", .5m) with { IsEnabled = false, Anchors = active.Anchors with { Three = "" } }
                 })
            Assert.Throws<EvaluationValidationException>(() => Profiles.Create(criteria: [active, disabled]));
    }

    [Fact]
    public void Add_remove_disable_and_reenable_create_versions_without_rewriting_history()
    {
        var original = Profiles.Create(criteria: [Profiles.Criterion("original", 1)], status: ProfileStatus.Active);
        var input = Profiles.Scores(original, 2);
        var snapshot = new EvaluationSnapshot(original.TenantId, "evaluation-1", original, original.Reference, input);
        var oldResult = JsonSerializer.Serialize(snapshot.Result);
        var oldContent = snapshot.Profile.CanonicalContent;
        var added = original.CreateNextVersion(
            [original.Criteria[0] with { Weight = .5m }, Profiles.Criterion("custom-new", .5m)],
            original.ComplexityBands, original.StoryPointBands, Profiles.Governance with { ChangeReason = "Add metric" });
        var disabled = added.CreateNextVersion(
            [added.Criteria[0] with { IsEnabled = false }, added.Criteria[1] with { Weight = 1 }],
            added.ComplexityBands, added.StoryPointBands, Profiles.Governance with { ChangeReason = "Disable original" });
        var removed = disabled.CreateNextVersion([disabled.Criteria[1]], disabled.ComplexityBands,
            null, Profiles.Governance with { ChangeReason = "Remove disabled and disable point mapping" });
        var reenabled = disabled.CreateNextVersion(disabled.Criteria.Select(c => c with { IsEnabled = true, Weight = .5m }),
            disabled.ComplexityBands, disabled.StoryPointBands, Profiles.Governance with { ChangeReason = "Reenable" });
        Assert.Equal(2, added.Version);
        Assert.Equal(3, disabled.Version);
        Assert.Equal(4, removed.Version);
        Assert.Equal(ProfileStatus.Draft, added.Status);
        Assert.Equal(ProfileStatus.Active, original.Status);
        Assert.Equal(2, reenabled.ActiveCriteria.Length);
        Assert.Single(removed.Criteria);
        Assert.Null(removed.StoryPointBands);
        Assert.Equal(5m, McdaEvaluator.Evaluate(disabled, Profiles.Scores(disabled, 5)).WeightedScore);
        Assert.Throws<EvaluationValidationException>(() => McdaEvaluator.Evaluate(disabled, Profiles.Scores(disabled, 5).Append(new("original", 3))));
        Assert.Equal(2m, snapshot.Result.WeightedScore);
        Assert.Equal(oldContent, snapshot.Profile.CanonicalContent);
        Assert.Equal(oldResult, JsonSerializer.Serialize(snapshot.Result));
        Assert.NotEqual(original.ContentHash, added.ContentHash);
        Assert.Throws<EvaluationValidationException>(() => McdaEvaluator.Evaluate(added, original.Reference, Profiles.Scores(added, 2, 5)));
    }

    [Fact]
    public void Membership_changes_require_explicit_rebalancing_and_do_not_normalize()
    {
        var p = Profiles.Create(weights: [.6m, .4m]);
        Assert.Throws<EvaluationValidationException>(() => p.CreateNextVersion([p.Criteria[0]], p.ComplexityBands,
            null, Profiles.Governance with { ChangeReason = "Remove without rebalance" }));
        Assert.Throws<EvaluationValidationException>(() => p.CreateNextVersion(
            [p.Criteria[0], p.Criteria[1] with { IsEnabled = false }], p.ComplexityBands, null,
            Profiles.Governance with { ChangeReason = "Disable without rebalance" }));
        Assert.Equal(new decimal[] { .6m, .4m }, p.Criteria.Select(c => c.Weight));
    }

    [Fact]
    public void Tenant_identity_is_required_and_part_of_hash_reference_and_snapshot()
    {
        var a = Profiles.Create(tenantId: "a", weights: [1]);
        var b = Profiles.Create(tenantId: "b", weights: [1]);
        Assert.Equal(a.ProfileId, b.ProfileId);
        Assert.NotEqual(a.ContentHash, b.ContentHash);
        Assert.NotEqual(a.Reference, b.Reference);
        Assert.Equal("a", McdaEvaluator.Evaluate(a, Profiles.Scores(a, 3)).Profile.TenantId);
        Assert.Contains(Assert.Throws<EvaluationValidationException>(() => Profiles.Create(tenantId: "")).Errors, e => e.Code == "profile.tenant");
        Assert.Throws<EvaluationValidationException>(() => McdaEvaluator.Evaluate(a, b.Reference, Profiles.Scores(a, 3)));
        Assert.Throws<EvaluationValidationException>(() => McdaEvaluator.Evaluate(a, a.Reference with { TenantId = "b" }, Profiles.Scores(a, 3)));
        Assert.Throws<EvaluationValidationException>(() => new EvaluationSnapshot("b", "e", a, a.Reference, Profiles.Scores(a, 3)));
        Assert.Throws<EvaluationValidationException>(() => new EvaluationSnapshot("a", "e", a, b.Reference, Profiles.Scores(a, 3)));
        Assert.Throws<EvaluationValidationException>(() => new EvaluationSnapshot("a", "", a, a.Reference, Profiles.Scores(a, 3)));
    }

    [Fact]
    public void Catalog_provenance_is_tenant_bound_and_contributes_to_hash()
    {
        var metric = StandardMetricTemplate.CreateCatalog("fixture-tenant").Definitions[0];
        var criterion = CriterionDefinition.FromMetric(metric, 1);
        var p = Profiles.Create(criteria: [criterion]);
        var next = metric.CreateNextVersion(metric.Label, metric.Description, metric.Anchors);
        Assert.NotEqual(p.ContentHash, Profiles.Create(criteria: [CriterionDefinition.FromMetric(next, 1)]).ContentHash);
        Assert.NotEqual(p.ContentHash, Profiles.Create(criteria: [criterion with { SourceMetric = null }]).ContentHash);
        foreach (var source in new[] { metric.Reference with { TenantId = "foreign" }, metric.Reference with { MetricId = "different" }, metric.Reference with { Version = 0 } })
            Assert.Throws<EvaluationValidationException>(() => Profiles.Create(criteria: [criterion with { SourceMetric = source }]));
    }

    [Fact]
    public void Disabled_membership_affects_hash_even_with_zero_weight()
    {
        var p = Profiles.Create(criteria: [Profiles.Criterion("on", 1), Profiles.Criterion("zero", 0)]);
        var q = Profiles.Create(criteria: [p.Criteria[0], p.Criteria[1] with { IsEnabled = false }]);
        Assert.NotEqual(p.ContentHash, q.ContentHash);
        Assert.Throws<EvaluationValidationException>(() => McdaEvaluator.Evaluate(p, [new("on", 3)]));
        Assert.Equal(3m, McdaEvaluator.Evaluate(q, [new("on", 3)]).WeightedScore);
    }

    [Fact]
    public void Evaluation_snapshot_captures_profile_scores_questions_and_result_immutably()
    {
        var p = Profiles.Create(weights: [.5m, .5m]);
        var scores = Profiles.Scores(p, 4, null);
        var snapshot = new EvaluationSnapshot(p.TenantId, "history", p, p.Reference, scores);
        scores[1] = scores[1] with { Score = 1, MissingInformation = "changed" };
        Assert.Null(snapshot.Scores[1].Score);
        Assert.Equal("Clarify c1", snapshot.Scores[1].MissingInformation);
        Assert.True(snapshot.Result.RequiresRefinement);
        Assert.Equal(new(2.5m, 4.5m, 37.5m, 87.5m), snapshot.Result.Bounds);
        Assert.Equal(p.CanonicalContent, snapshot.Profile.CanonicalContent);
        Assert.Throws<NotSupportedException>(() => ((IList)snapshot.Scores)[0] = new CriterionScore("c0", 1));
        Assert.All(typeof(EvaluationSnapshot).GetProperties(), property => Assert.Null(property.SetMethod));
    }
}
