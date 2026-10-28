using System.Collections.Immutable;
using System.Numerics;

namespace TaskMetricAI.Evaluation;

public enum PerturbationDirection { Decrease, Increase }
public enum SensitivityStatus { Calculated, NotApplicable }
public enum SensitivitySkipReason { ZeroTargetWeight, NoOtherPositiveWeight, TargetOutsideDomain }
public sealed record ScenarioWeight(string CriterionId, decimal Weight);
public sealed record SensitivityScenario(
    string TargetCriterionId, PerturbationDirection Direction, SensitivityStatus Status,
    SensitivitySkipReason? SkipReason, ImmutableArray<ScenarioWeight> Weights,
    EvaluationResult? HypotheticalResult, bool? ClassificationChanged, bool? PointsChanged);
public sealed record SensitivityReport(
    string PolicyVersion, int WeightFractionalDigits, EvaluationResult Baseline,
    ImmutableArray<SensitivityScenario> Scenarios, bool? IsStable);

/// <summary>Hypothetical scenarios only; never changes configured profile content or authorization.</summary>
public static class SensitivityAnalyzer
{
    private const long Units = 1_000_000_000_000;

    public static SensitivityReport Analyze(EvaluationProfile profile, IEnumerable<CriterionScore> scores)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Analyze(profile, profile.Reference, scores);
    }

    public static SensitivityReport Analyze(EvaluationProfile profile, ProfileReference expectedProfile,
        IEnumerable<CriterionScore> scores)
    {
        var accepted = McdaEvaluator.ValidateInputs(profile, expectedProfile, scores);
        var weights = profile.ActiveCriteria.Select(c => c.Weight).ToImmutableArray();
        var baseline = McdaEvaluator.Calculate(profile, accepted, weights);
        var originalUnits = weights.Select(w => decimal.ToInt64(w * Units)).ToArray();
        var scenarios = ImmutableArray.CreateBuilder<SensitivityScenario>();
        for (var target = 0; target < weights.Length; target++)
        {
            foreach (var direction in new[] { PerturbationDirection.Decrease, PerturbationDirection.Increase })
            {
                var targetUnits = originalUnits[target] * (direction == PerturbationDirection.Decrease ? 9 : 11) / 10;
                SensitivitySkipReason? skip = originalUnits[target] == 0 ? SensitivitySkipReason.ZeroTargetWeight
                    : originalUnits[target] == Units ? SensitivitySkipReason.NoOtherPositiveWeight
                    : targetUnits > Units ? SensitivitySkipReason.TargetOutsideDomain : null;
                if (skip is not null)
                {
                    scenarios.Add(new(profile.ActiveCriteria[target].CriterionId, direction, SensitivityStatus.NotApplicable,
                        skip, [], null, null, null));
                    continue;
                }
                var allocated = Allocate(profile, originalUnits, target, targetUnits);
                var scenarioWeights = allocated.Select(u => (decimal)u / Units).ToImmutableArray();
                var result = McdaEvaluator.Calculate(profile, accepted, scenarioWeights);
                bool definitive = !baseline.RequiresRefinement && !result.RequiresRefinement;
                scenarios.Add(new(profile.ActiveCriteria[target].CriterionId, direction, SensitivityStatus.Calculated, null,
                    profile.ActiveCriteria.Select((c, i) => new ScenarioWeight(c.CriterionId, scenarioWeights[i])).ToImmutableArray(),
                    result, definitive ? result.Classification?.BandId != baseline.Classification?.BandId : null,
                    definitive ? (result.PointSuggestion?.Points, result.PointSuggestion?.RecommendSplit) !=
                        (baseline.PointSuggestion?.Points, baseline.PointSuggestion?.RecommendSplit) : null));
            }
        }
        bool? stable = !baseline.RequiresRefinement && scenarios.Any(s => s.Status == SensitivityStatus.Calculated)
            ? !scenarios.Any(s => s.ClassificationChanged == true || s.PointsChanged == true) : null;
        return new(profile.Policy.SensitivityVersion, 12, baseline, scenarios.ToImmutable(), stable);
    }

    private static long[] Allocate(EvaluationProfile profile, long[] original, int target, long targetUnits)
    {
        var result = new long[original.Length];
        result[target] = targetUnits;
        long remaining = Units - targetUnits;
        long denominator = Units - original[target];
        var fractions = new List<(int Index, BigInteger Remainder)>();
        for (var i = 0; i < original.Length; i++)
        {
            if (i == target) continue;
            var quotient = BigInteger.DivRem((BigInteger)original[i] * remaining, denominator, out var remainder);
            result[i] = (long)quotient;
            fractions.Add((i, remainder));
        }
        var leftover = Units - result.Sum();
        foreach (var fraction in fractions.OrderByDescending(f => f.Remainder)
                     .ThenBy(f => profile.ActiveCriteria[f.Index].CriterionId, StringComparer.Ordinal).Take((int)leftover))
            result[fraction.Index]++;
        return result;
    }
}
