using System.Collections.Immutable;

namespace TaskMetricAI.Evaluation;

public sealed record CriterionScore(string CriterionId, int? Score, string? MissingInformation = null);
public sealed record CriterionContribution(
    string CriterionId, decimal Weight, int? Score, decimal? Contribution, string? MissingInformation);
public sealed record ScoreBounds(decimal Lower, decimal Upper, decimal NormalizedLower, decimal NormalizedUpper);

public sealed record EvaluationResult(
    ProfileReference Profile, string ScoringPolicyVersion, bool RequiresRefinement,
    decimal? WeightedScore, decimal? NormalizedIndex, ComplexityBand? Classification,
    StoryPointBand? PointSuggestion, ScoreBounds Bounds,
    ImmutableArray<CriterionContribution> Contributions, ImmutableArray<CriterionScore> UnknownCriteria);

/// <summary>Pure scoring; does not resolve scope, authorize actors, activate profiles or publish results.</summary>
public static class McdaEvaluator
{
    public static EvaluationResult Evaluate(EvaluationProfile profile, IEnumerable<CriterionScore> scores)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Evaluate(profile, profile.Reference, scores);
    }

    public static EvaluationResult Evaluate(EvaluationProfile profile, ProfileReference expectedProfile,
        IEnumerable<CriterionScore> scores)
    {
        var accepted = ValidateInputs(profile, expectedProfile, scores);
        return Calculate(profile, accepted, profile.ActiveCriteria.Select(c => c.Weight).ToImmutableArray());
    }

    internal static ImmutableArray<CriterionScore> ValidateInputs(EvaluationProfile profile,
        ProfileReference expectedProfile, IEnumerable<CriterionScore> scores)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(expectedProfile);
        ArgumentNullException.ThrowIfNull(scores);
        var errors = ImmutableArray.CreateBuilder<ValidationError>();
        if (expectedProfile != profile.Reference)
            errors.Add(new("assessment.profile", "Profile", "Profile ID/version/content hash mismatch."));
        var map = new Dictionary<string, CriterionScore>(StringComparer.Ordinal);
        var ids = profile.ActiveCriteria.Select(c => c.CriterionId).ToHashSet(StringComparer.Ordinal);
        foreach (var score in scores)
        {
            if (score is null || string.IsNullOrWhiteSpace(score.CriterionId))
            {
                errors.Add(new("assessment.id", "Scores", "Nonempty criterion ID required."));
                continue;
            }
            if (!map.TryAdd(score.CriterionId, score))
                errors.Add(new("assessment.duplicate", score.CriterionId, "Duplicate assessment."));
            if (!ids.Contains(score.CriterionId))
                errors.Add(new("assessment.unexpected", score.CriterionId, "Criterion is not enabled in this profile."));
            if (score.Score is < 1 or > 5)
                errors.Add(new("assessment.range", score.CriterionId, "Score must be integer 1–5 or null."));
        }
        foreach (var id in profile.ActiveCriteria.Select(c => c.CriterionId))
            if (!map.ContainsKey(id)) errors.Add(new("assessment.missing", id, "Provide a score or explicit unknown."));
        if (errors.Count != 0) throw new EvaluationValidationException(errors);
        return profile.ActiveCriteria.Select(c => map[c.CriterionId]).ToImmutableArray();
    }

    internal static EvaluationResult Calculate(EvaluationProfile profile, ImmutableArray<CriterionScore> scores,
        ImmutableArray<decimal> weights)
    {
        var contributions = ImmutableArray.CreateBuilder<CriterionContribution>();
        var unknowns = ImmutableArray.CreateBuilder<CriterionScore>();
        decimal lower = 0, upper = 0;
        bool incomplete = false;
        for (var i = 0; i < scores.Length; i++)
        {
            var score = scores[i];
            var weight = weights[i];
            decimal? contribution = score.Score is { } known ? weight * known : weight == 0 ? 0m : null;
            contributions.Add(new(score.CriterionId, weight, score.Score, contribution, score.MissingInformation));
            lower += weight * (score.Score ?? 1);
            upper += weight * (score.Score ?? 5);
            if (score.Score is null)
            {
                unknowns.Add(score);
                incomplete |= weight > 0;
            }
        }
        var bounds = new ScoreBounds(lower, upper, Normalize(lower), Normalize(upper));
        return new(profile.Reference, profile.Policy.ScoringVersion, incomplete,
            incomplete ? null : lower, incomplete ? null : Normalize(lower),
            incomplete ? null : profile.ComplexityBands.Single(b => b.Interval.Contains(lower)),
            incomplete ? null : profile.StoryPointBands?.Single(b => b.Interval.Contains(lower)),
            bounds, contributions.ToImmutable(), unknowns.ToImmutable());
    }

    private static decimal Normalize(decimal score) => 25m * (score - 1m);
}
