using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Evaluation.Tests;

public sealed class PropertyTests
{
    [Fact]
    public void Generated_profiles_obey_bounds_contribution_and_monotonicity_properties()
    {
        var random = new Random(20261027);
        for (var iteration = 0; iteration < 300; iteration++)
        {
            var count = random.Next(2, 10);
            var weights = Weights(random, count);
            var p = Profiles.Create(weights: weights);
            var scores = Enumerable.Range(0, count).Select(_ => (int?)random.Next(1, 6)).ToArray();
            var result = McdaEvaluator.Evaluate(p, Profiles.Scores(p, scores));
            Assert.InRange(result.WeightedScore!.Value, 1m, 5m);
            Assert.InRange(result.NormalizedIndex!.Value, 0m, 100m);
            Assert.Equal(result.WeightedScore, result.Contributions.Sum(c => c.Contribution));
            Assert.Equal(result.Bounds.Lower, result.Bounds.Upper);
            for (var i = 0; i < count; i++)
            {
                if (scores[i] == 5) continue;
                var increased = (int?[])scores.Clone();
                increased[i]++;
                var higher = McdaEvaluator.Evaluate(p, Profiles.Scores(p, increased));
                Assert.Equal(result.WeightedScore + weights[i], higher.WeightedScore);
            }
        }
    }

    [Fact]
    public void Unknown_bounds_contain_every_completion()
    {
        var p = Profiles.Create(weights: [.2m, .3m, .5m]);
        var unknown = McdaEvaluator.Evaluate(p, Profiles.Scores(p, null, 3, null));
        for (var a = 1; a <= 5; a++)
            for (var b = 1; b <= 5; b++)
            {
                var completed = McdaEvaluator.Evaluate(p, Profiles.Scores(p, a, 3, b));
                Assert.InRange(completed.WeightedScore!.Value, unknown.Bounds.Lower, unknown.Bounds.Upper);
            }
        Assert.Equal(unknown.Bounds.Lower, McdaEvaluator.Evaluate(p, Profiles.Scores(p, 1, 3, 1)).WeightedScore);
        Assert.Equal(unknown.Bounds.Upper, McdaEvaluator.Evaluate(p, Profiles.Scores(p, 5, 3, 5)).WeightedScore);
    }

    [Fact]
    public void Generated_sensitivity_weights_sum_exactly_to_one_and_keep_all_bounds()
    {
        var random = new Random(41027);
        for (var iteration = 0; iteration < 100; iteration++)
        {
            var count = random.Next(2, 12);
            var p = Profiles.Create(weights: Weights(random, count));
            var report = SensitivityAnalyzer.Analyze(p, Profiles.Scores(p,
                Enumerable.Range(0, count).Select(_ => (int?)random.Next(1, 6)).ToArray()));
            foreach (var scenario in report.Scenarios.Where(s => s.Status == SensitivityStatus.Calculated))
            {
                Assert.Equal(1m, scenario.Weights.Sum(w => w.Weight));
                Assert.All(scenario.Weights, w =>
                {
                    Assert.InRange(w.Weight, 0m, 1m);
                    Assert.Equal(w.Weight, decimal.Round(w.Weight, 12));
                });
                Assert.InRange(scenario.HypotheticalResult!.WeightedScore!.Value, 1m, 5m);
                Assert.Equal(scenario.HypotheticalResult.WeightedScore,
                    scenario.HypotheticalResult.Contributions.Sum(c => c.Contribution));
            }
        }
    }

    private static decimal[] Weights(Random random, int count)
    {
        var cuts = new[] { 0, 10000 }.Concat(Enumerable.Range(0, count - 1).Select(_ => random.Next(0, 10001))).Order().ToArray();
        return Enumerable.Range(0, count).Select(i => (cuts[i + 1] - cuts[i]) / 10000m).ToArray();
    }
}
