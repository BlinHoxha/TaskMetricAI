using System.Text.Json;
using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Evaluation.Tests;

public sealed class SensitivityTests
{
    [Fact]
    public void Allocates_exact_recurring_fractions_and_residual_by_ordinal_ID()
    {
        var p = Profiles.Create(criteria: [Profiles.Criterion("target", .1m), Profiles.Criterion("z", .3m),
            Profiles.Criterion("a", .3m), Profiles.Criterion("b", .3m)]);
        var hash = p.ContentHash;
        var report = SensitivityAnalyzer.Analyze(p, Profiles.Scores(p, 1, 2, 3, 4));
        Assert.Equal("relative-10-v1", report.PolicyVersion);
        Assert.Equal(12, report.WeightFractionalDigits);
        var down = report.Scenarios.Single(s => s.TargetCriterionId == "target" && s.Direction == PerturbationDirection.Decrease);
        Assert.Equal(new decimal[] { .09m, .303333333333m, .303333333334m, .303333333333m }, down.Weights.Select(w => w.Weight));
        var up = report.Scenarios.Single(s => s.TargetCriterionId == "target" && s.Direction == PerturbationDirection.Increase);
        Assert.Equal(new decimal[] { .11m, .296666666666m, .296666666667m, .296666666667m }, up.Weights.Select(w => w.Weight));
        Assert.Equal(1m, down.Weights.Sum(w => w.Weight));
        Assert.Equal(1m, up.Weights.Sum(w => w.Weight));
        Assert.Equal(hash, p.ContentHash);
        Assert.Equal(new decimal[] { .1m, .3m, .3m, .3m }, p.Criteria.Select(c => c.Weight));
        Assert.Equal(p.Reference, down.HypotheticalResult!.Profile);
    }

    [Fact]
    public void Residual_allocation_does_not_depend_on_criterion_input_order()
    {
        var criteria = new[] { Profiles.Criterion("target", .1m), Profiles.Criterion("z", .3m),
            Profiles.Criterion("a", .3m), Profiles.Criterion("b", .3m) };
        var a = Profiles.Create(criteria: criteria);
        var b = Profiles.Create(criteria: criteria.Reverse().ToArray());
        var sa = SensitivityAnalyzer.Analyze(a, criteria.Select(c => new CriterionScore(c.CriterionId, 3)));
        var sb = SensitivityAnalyzer.Analyze(b, criteria.Select(c => new CriterionScore(c.CriterionId, 3)));
        foreach (var scenario in sa.Scenarios)
        {
            var other = sb.Scenarios.Single(s => s.TargetCriterionId == scenario.TargetCriterionId && s.Direction == scenario.Direction);
            Assert.Equal(scenario.Weights.OrderBy(w => w.CriterionId, StringComparer.Ordinal), other.Weights.OrderBy(w => w.CriterionId, StringComparer.Ordinal));
        }
    }

    [Fact]
    public void Degenerate_cases_are_explicit_without_clamping()
    {
        var p = Profiles.Create(weights: [1m, 0m]);
        var r = SensitivityAnalyzer.Analyze(p, Profiles.Scores(p, 3, null));
        Assert.All(r.Scenarios.Where(s => s.TargetCriterionId == "c0"), s => Assert.Equal(SensitivitySkipReason.NoOtherPositiveWeight, s.SkipReason));
        Assert.All(r.Scenarios.Where(s => s.TargetCriterionId == "c1"), s => Assert.Equal(SensitivitySkipReason.ZeroTargetWeight, s.SkipReason));
        Assert.All(r.Scenarios, s =>
        {
            Assert.Equal(SensitivityStatus.NotApplicable, s.Status);
            Assert.Null(s.HypotheticalResult);
            Assert.Empty(s.Weights);
        });
        Assert.Null(r.IsStable);
        p = Profiles.Create(weights: [.95m, .05m]);
        r = SensitivityAnalyzer.Analyze(p, Profiles.Scores(p, 3, 4));
        var over = r.Scenarios.Single(s => s.TargetCriterionId == "c0" && s.Direction == PerturbationDirection.Increase);
        Assert.Equal(SensitivitySkipReason.TargetOutsideDomain, over.SkipReason);
        Assert.Null(over.HypotheticalResult);
        Assert.Equal(3, r.Scenarios.Count(s => s.Status == SensitivityStatus.Calculated));
    }

    [Fact]
    public void Zero_recipients_remain_zero()
    {
        var p = Profiles.Create(weights: [.5m, .5m, 0m]);
        var r = SensitivityAnalyzer.Analyze(p, Profiles.Scores(p, 1, 4, null));
        foreach (var scenario in r.Scenarios.Where(s => s.Status == SensitivityStatus.Calculated))
        {
            Assert.Equal(0m, scenario.Weights[2].Weight);
            Assert.False(scenario.HypotheticalResult!.RequiresRefinement);
        }
    }

    [Fact]
    public void Reports_instability_at_threshold_and_unknown_stability_for_incomplete_scores()
    {
        var p = Profiles.Create(weights: [.5m, .5m]);
        var r = SensitivityAnalyzer.Analyze(p, Profiles.Scores(p, 1, 2));
        Assert.False(r.IsStable);
        Assert.Contains(r.Scenarios, s => s.ClassificationChanged == true && s.PointsChanged == true);
        var uniform = SensitivityAnalyzer.Analyze(p, Profiles.Scores(p, 3, 3));
        Assert.True(uniform.IsStable);
        var partial = SensitivityAnalyzer.Analyze(p, Profiles.Scores(p, 1, null));
        Assert.Null(partial.IsStable);
        Assert.All(partial.Scenarios, s =>
        {
            Assert.True(s.HypotheticalResult!.RequiresRefinement);
            Assert.Null(s.ClassificationChanged);
            Assert.Null(s.PointsChanged);
            Assert.Null(s.HypotheticalResult.PointSuggestion);
        });
    }

    [Fact]
    public void Six_digit_configured_weights_have_exact_seven_digit_targets()
    {
        var p = Profiles.Create(weights: [.123456m, .876544m], policy: new(4));
        var r = SensitivityAnalyzer.Analyze(p, Profiles.Scores(p, 2, 5));
        Assert.Equal(.1111104m, r.Scenarios[0].Weights[0].Weight);
        Assert.Equal(.1358016m, r.Scenarios[1].Weights[0].Weight);
    }

    [Fact]
    public void Sensitivity_validates_scores_and_identity_before_scenarios()
    {
        var p = Profiles.Create(weights: [.5m, .5m]);
        Assert.Throws<EvaluationValidationException>(() => SensitivityAnalyzer.Analyze(p, []));
        Assert.Throws<EvaluationValidationException>(() => SensitivityAnalyzer.Analyze(p, p.Reference with { Version = 2 }, Profiles.Scores(p, 1, 2)));
    }

    [Fact]
    public void Repeated_sensitivity_results_are_identical()
    {
        var p = Profiles.Create();
        var scores = Profiles.Scores(p, 4, 3, 4, 3, 5, 3, 5, 2);
        Assert.Equal(JsonSerializer.Serialize(SensitivityAnalyzer.Analyze(p, scores)),
            JsonSerializer.Serialize(SensitivityAnalyzer.Analyze(p, scores)));
    }
}
