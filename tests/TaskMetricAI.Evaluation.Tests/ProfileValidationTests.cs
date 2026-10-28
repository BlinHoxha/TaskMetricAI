using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Evaluation.Tests;

public sealed class ProfileValidationTests
{
    private static void Invalid(string code, Action action) =>
        Assert.Contains(Assert.Throws<EvaluationValidationException>(action).Errors, e => e.Code == code);

    [Theory]
    [InlineData("0.99")]
    [InlineData("1.01")]
    [InlineData("0")]
    public void Rejects_invalid_totals_without_repair(string total) =>
        Invalid("weights.total", () => Profiles.Create(weights: [decimal.Parse(total, System.Globalization.CultureInfo.InvariantCulture)]));

    [Theory]
    [InlineData("-0.01", "1.01")]
    [InlineData("1.01", "-0.01")]
    public void Rejects_out_of_range_even_if_sum_is_one(string a, string b) =>
        Invalid("weight.range", () => Profiles.Create(weights: [decimal.Parse(a, System.Globalization.CultureInfo.InvariantCulture), decimal.Parse(b, System.Globalization.CultureInfo.InvariantCulture)]));

    [Fact]
    public void Rejects_empty_duplicate_and_blank_criteria()
    {
        Invalid("criteria.empty", () => Profiles.Create(criteria: []));
        Invalid("criterion.id", () => Profiles.Create(criteria: [Profiles.Criterion("same", .5m), Profiles.Criterion("same", .5m)]));
        Invalid("criterion.id", () => Profiles.Create(criteria: [Profiles.Criterion(" ", 1)]));
        Invalid("criterion.description", () => Profiles.Create(criteria: [Profiles.Criterion("c", 1) with { Definition = "" }]));
    }

    [Fact]
    public void Requires_all_five_anchors()
    {
        var c = Profiles.Criterion("c", 1);
        var a = c.Anchors;
        foreach (var anchors in new[] { a with { One = "" }, a with { Two = " " }, a with { Three = null! },
                     a with { Four = "" }, a with { Five = "" }, null! })
            Invalid("criterion.anchors", () => Profiles.Create(criteria: [c with { Anchors = anchors }]));
    }

    [Fact]
    public void Percentage_precision_is_configured_without_rounding()
    {
        Invalid("weight.precision", () => Profiles.Create(weights: [.50001m, .49999m]));
        Assert.Empty(ProfileValidator.Validate(Profiles.Create(weights: [.50001m, .49999m], policy: new(3))));
        Invalid("weight.precision", () => Profiles.Create(weights: [.501m, .499m], policy: new(0)));
        Assert.Empty(ProfileValidator.Validate(Profiles.Create(weights: [.500000m, .500000m], policy: new(0))));
        Assert.Empty(ProfileValidator.Validate(Profiles.Create(weights: [.123456m, .876544m], policy: new(4))));
        Invalid("policy.precision", () => Profiles.Create(policy: new(-1)));
        Invalid("policy.precision", () => Profiles.Create(policy: new(5)));
        Invalid("policy.version", () => Profiles.Create(policy: new(ScoringVersion: "future")));
        Invalid("policy.version", () => Profiles.Create(policy: new(SensitivityVersion: "future")));
    }

    public static IEnumerable<object[]> InvalidBands()
    {
        yield return [Array.Empty<ComplexityBand>()];
        yield return [new[] { new ComplexityBand("a", "A", new(1.01m, 5, true, true)) }];
        yield return [new[] { new ComplexityBand("a", "A", new(1, 5, false, true)) }];
        yield return [new[] { new ComplexityBand("a", "A", new(1, 5)) }];
        yield return [new[] { new ComplexityBand("a", "A", new(0, 5, true, true)) }];
        yield return [new[] { new ComplexityBand("a", "A", new(1, 6, true, true)) }];
        yield return [new[] { new ComplexityBand("a", "A", new(1, 1)), new ComplexityBand("b", "B", new(1, 5, true, true)) }];
        yield return [new[] { new ComplexityBand("a", "A", new(1, 2)), new ComplexityBand("b", "B", new(2.01m, 5, true, true)) }];
        yield return [new[] { new ComplexityBand("a", "A", new(1, 3)), new ComplexityBand("b", "B", new(2, 5, true, true)) }];
        yield return [new[] { new ComplexityBand("a", "A", new(1, 2, true, true)), new ComplexityBand("b", "B", new(2, 5, true, true)) }];
        yield return [new[] { new ComplexityBand("a", "A", new(1, 2)), new ComplexityBand("b", "B", new(2, 5, false, true)) }];
        yield return [new[] { new ComplexityBand("same", "A", new(1, 2)), new ComplexityBand("same", "B", new(2, 5, true, true)) }];
        yield return [new[] { new ComplexityBand("a", "", new(1, 5, true, true)) }];
        yield return [new[] { new ComplexityBand("a", "A", null!) }];
        yield return [Profiles.Bands.Reverse().ToArray()];
    }

    [Theory]
    [MemberData(nameof(InvalidBands))]
    public void Rejects_invalid_classification_intervals(ComplexityBand[] bands) =>
        Assert.Throws<EvaluationValidationException>(() => Profiles.Create(bands: bands));

    [Fact]
    public void Validates_optional_point_mapping_independently()
    {
        Assert.Null(Profiles.Create(points: false).StoryPointBands);
        Invalid("bands.empty", () => Profiles.Create(pointBands: []));
        Invalid("points.range", () => Profiles.Create(pointBands: [new("x", new(1, 5, true, true), 0)]));
        Invalid("bands.endpoints", () => Profiles.Create(pointBands: [new("x", new(2, 5, true, true), 3)]));
        foreach (var invalid in InvalidBands().Select(x => (ComplexityBand[])x[0]).Where(b => b.All(x => !string.IsNullOrEmpty(x.Name))))
            Assert.Throws<EvaluationValidationException>(() => Profiles.Create(pointBands:
                invalid.Select(b => new StoryPointBand(b.BandId, b.Interval, 3)).ToArray()));
    }

    [Fact]
    public void Validates_identity_scope_and_metadata()
    {
        Invalid("profile.id", () => Profiles.Create(id: ""));
        Invalid("profile.version", () => Profiles.Create(version: 0));
        Invalid("profile.name", () => Profiles.Create(name: " "));
        Invalid("profile.scope", () => Profiles.Create(scope: new((ScopeType)99, "x")));
        Invalid("profile.scope", () => Profiles.Create(scope: new(ScopeType.Team, "")));
        Invalid("profile.status", () => Profiles.Create(status: (ProfileStatus)99));
        Invalid("profile.governance", () => Profiles.Create(governance: Profiles.Governance with { CreatedBy = "" }));
        Invalid("profile.approval", () => Profiles.Create(governance: Profiles.Governance with { ApprovedBy = "admin" }));
    }

    [Fact]
    public void IDs_are_ordinal_and_case_sensitive() => Assert.Empty(ProfileValidator.Validate(
        Profiles.Create(criteria: [Profiles.Criterion("A", .5m), Profiles.Criterion("a", .5m)])));
}
