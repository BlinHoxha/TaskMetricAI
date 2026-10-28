using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Evaluation.Tests;

public sealed class ScoringTests
{
    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(3, 3, 50)]
    [InlineData(5, 5, 100)]
    public void Reference_uniform_vectors(int value, int score, int index)
    {
        var profile = Profiles.Create();
        var result = McdaEvaluator.Evaluate(profile, Profiles.Scores(profile, Enumerable.Repeat<int?>(value, 8).ToArray()));
        Assert.Equal((decimal)score, result.WeightedScore);
        Assert.Equal((decimal)index, result.NormalizedIndex);
    }

    [Fact]
    public void Documented_vector_is_reproducible_with_contributions_and_identity()
    {
        var p = Profiles.Create();
        var r = McdaEvaluator.Evaluate(p, p.Reference, Profiles.Scores(p, 4, 3, 4, 3, 5, 3, 5, 2));
        Assert.Equal(3.80m, r.WeightedScore);
        Assert.Equal(70m, r.NormalizedIndex);
        Assert.Equal("high", r.Classification!.BandId);
        Assert.Equal(8, r.PointSuggestion!.Points);
        Assert.Equal(p.Reference, r.Profile);
        Assert.Equal("wsm-v1", r.ScoringPolicyVersion);
        Assert.Equal(new decimal?[] { .8m, .45m, .6m, .3m, .75m, .3m, .5m, .1m }, r.Contributions.Select(c => c.Contribution));
        Assert.Equal(3.8m, r.Contributions.Sum(c => c.Contribution));
        Assert.False(r.RequiresRefinement);
        Assert.Empty(r.UnknownCriteria);
    }

    [Fact]
    public void Human_defined_weights_and_bands_change_results_without_code_changes()
    {
        var a = Profiles.Create(weights: [.8m, .2m]);
        var b = Profiles.Create(weights: [.2m, .8m], bands: [new("custom", "Team terminology", new(1, 5, true, true))],
            pointBands: [new("custom-points", new(1, 5, true, true), 21, true)]);
        Assert.Equal(1.8m, McdaEvaluator.Evaluate(a, Profiles.Scores(a, 1, 5)).WeightedScore);
        var result = McdaEvaluator.Evaluate(b, Profiles.Scores(b, 1, 5));
        Assert.Equal(4.2m, result.WeightedScore);
        Assert.Equal("custom", result.Classification!.BandId);
        Assert.Equal(21, result.PointSuggestion!.Points);
        Assert.True(result.PointSuggestion.RecommendSplit);
        Assert.NotEqual(a.ContentHash, b.ContentHash);
    }

    [Fact]
    public void Unknown_positive_weight_returns_bounds_and_preserves_question()
    {
        var p = Profiles.Create(weights: [.25m, .75m]);
        var r = McdaEvaluator.Evaluate(p, Profiles.Scores(p, 4, null));
        Assert.True(r.RequiresRefinement);
        Assert.Null(r.WeightedScore);
        Assert.Null(r.NormalizedIndex);
        Assert.Null(r.Classification);
        Assert.Null(r.PointSuggestion);
        Assert.Equal(new(1.75m, 4.75m, 18.75m, 93.75m), r.Bounds);
        Assert.Equal("Clarify c1", Assert.Single(r.UnknownCriteria).MissingInformation);
        Assert.Null(r.Contributions[1].Contribution);
    }

    [Fact]
    public void All_unknown_is_full_range_and_zero_weight_unknown_does_not_block()
    {
        var p = Profiles.Create(weights: [1m, 0m]);
        var unknown = McdaEvaluator.Evaluate(p, Profiles.Scores(p, null, null));
        Assert.Equal(new(1, 5, 0, 100), unknown.Bounds);
        Assert.Equal(2, unknown.UnknownCriteria.Length);
        var known = McdaEvaluator.Evaluate(p, Profiles.Scores(p, 3, null));
        Assert.False(known.RequiresRefinement);
        Assert.Equal(3m, known.WeightedScore);
        Assert.Equal(0m, known.Contributions[1].Contribution);
        Assert.Null(Assert.Single(known.UnknownCriteria).Score);
    }

    [Fact]
    public void Points_can_be_disabled_and_maximum_suggests_splitting()
    {
        var p = Profiles.Create(weights: [1], points: false);
        Assert.Null(McdaEvaluator.Evaluate(p, Profiles.Scores(p, 5)).PointSuggestion);
        p = Profiles.Create(weights: [1]);
        var r = McdaEvaluator.Evaluate(p, Profiles.Scores(p, 5));
        Assert.Equal(13, r.PointSuggestion!.Points);
        Assert.True(r.PointSuggestion.RecommendSplit);
    }

    [Theory]
    [InlineData("1", 1, "very-low")]
    [InlineData("1.495", 1, "very-low")]
    [InlineData("1.4999", 1, "very-low")]
    [InlineData("1.5", 2, "low")]
    [InlineData("1.5001", 2, "low")]
    [InlineData("1.9999", 2, "low")]
    [InlineData("2", 3, "moderate")]
    [InlineData("2.0001", 3, "moderate")]
    [InlineData("2.4999", 3, "moderate")]
    [InlineData("2.5", 5, "moderate")]
    [InlineData("2.5001", 5, "moderate")]
    [InlineData("2.9999", 5, "moderate")]
    [InlineData("3", 5, "high")]
    [InlineData("3.0001", 5, "high")]
    [InlineData("3.2499", 5, "high")]
    [InlineData("3.25", 8, "high")]
    [InlineData("3.2501", 8, "high")]
    [InlineData("3.9999", 8, "high")]
    [InlineData("4", 13, "very-high")]
    [InlineData("4.0001", 13, "very-high")]
    [InlineData("5", 13, "very-high")]
    public void Thresholds_use_unrounded_scores(string value, int points, string band)
    {
        decimal score = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        decimal highWeight = (score - 1m) / 4m;
        var p = Profiles.Create(weights: [1m - highWeight, highWeight], policy: new(4));
        var r = McdaEvaluator.Evaluate(p, Profiles.Scores(p, 1, 5));
        Assert.Equal(score, r.WeightedScore);
        Assert.Equal(points, r.PointSuggestion!.Points);
        Assert.Equal(band, r.Classification!.BandId);
    }

    [Fact]
    public void Inclusive_left_boundary_is_configurable()
    {
        var p = Profiles.Create(weights: [.5m, .5m], bands:
            [new("left", "Left", new(1, 2, true, true)), new("right", "Right", new(2, 5, false, true))]);
        Assert.Equal("left", McdaEvaluator.Evaluate(p, Profiles.Scores(p, 1, 3)).Classification!.BandId);
    }

    [Fact]
    public void Rejects_bad_assessment_sets_and_profile_identity()
    {
        var p = Profiles.Create(weights: [.5m, .5m]);
        CriterionScore[][] invalid = [[], [new("c0", 1)], [new("c0", 1), new("c0", 2)],
            [new("c0", 1), new("c1", 3), new("other", 1)], [new("c0", 0), new("c1", 3)],
            [new("c0", 6), new("c1", 3)], [new("C0", 1), new("c1", 3)], [null!, new("c1", 3)]];
        foreach (var scores in invalid) Assert.Throws<EvaluationValidationException>(() => McdaEvaluator.Evaluate(p, scores));
        foreach (var reference in new[] { p.Reference with { Version = 2 }, p.Reference with { ProfileId = "other" }, p.Reference with { ContentHash = new string('0', 64) } })
            Assert.Throws<EvaluationValidationException>(() => McdaEvaluator.Evaluate(p, reference, Profiles.Scores(p, 1, 3)));
        // Fractional values are not representable by the nullable-int scoring contract.
        Assert.Equal(typeof(int?), typeof(CriterionScore).GetProperty(nameof(CriterionScore.Score))!.PropertyType);
    }

    [Fact]
    public void Reordered_assessments_and_lifecycle_metadata_do_not_change_math()
    {
        var p = Profiles.Create(weights: [.4m, .6m]);
        var scores = Profiles.Scores(p, 2, 5);
        var a = McdaEvaluator.Evaluate(p, scores);
        var b = McdaEvaluator.Evaluate(p, scores.Reverse());
        Assert.Equal(a.WeightedScore, b.WeightedScore);
        Assert.Equal(a.Contributions.ToArray(), b.Contributions.ToArray());
        var archived = Profiles.Create(weights: [.4m, .6m], status: ProfileStatus.Archived);
        Assert.Equal(a.WeightedScore, McdaEvaluator.Evaluate(archived, scores).WeightedScore);
    }
}
