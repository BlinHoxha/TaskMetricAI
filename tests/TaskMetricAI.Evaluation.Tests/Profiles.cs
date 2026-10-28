using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Evaluation.Tests;

internal static class Profiles
{
    internal static readonly decimal[] ExampleWeights = [.20m, .15m, .15m, .10m, .15m, .10m, .10m, .05m];
    internal static ProfileGovernance Governance => new("fixture-admin", new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero), "Illustrative test policy");

    // Illustrative fixtures only; no production profile is seeded or activated.
    internal static CriterionDefinition Criterion(string id, decimal weight) => new(id, id, $"Assess {id} changes", weight,
        new($"No {id} changes", $"Localized known {id} change", $"Several {id} paths",
            $"Broad {id} impact", $"Cross-cutting {id} coordination"));

    internal static ComplexityBand[] Bands =>
    [
        new("very-low", "Very Low", new(1, 1.5m)),
        new("low", "Low", new(1.5m, 2)),
        new("moderate", "Moderate", new(2, 3)),
        new("high", "High", new(3, 4)),
        new("very-high", "Very High", new(4, 5, true, true))
    ];

    internal static StoryPointBand[] Points =>
    [
        new("p1", new(1, 1.5m), 1), new("p2", new(1.5m, 2), 2),
        new("p3", new(2, 2.5m), 3), new("p5", new(2.5m, 3.25m), 5),
        new("p8", new(3.25m, 4), 8), new("p13", new(4, 5, true, true), 13, true)
    ];

    internal static EvaluationProfile Create(decimal[]? weights = null, CriterionDefinition[]? criteria = null,
        ComplexityBand[]? bands = null, StoryPointBand[]? pointBands = null, bool points = true,
        ScoringPolicy? policy = null, string id = "fixture", int version = 1,
        ProfileScope? scope = null, ProfileStatus status = ProfileStatus.Draft, ProfileGovernance? governance = null,
        string name = "Illustrative profile", string description = "Not production policy", string tenantId = "fixture-tenant") =>
        new(tenantId, id, version, name, description, scope ?? new(ScopeType.Team, "fixture-team"),
            criteria ?? (weights ?? ExampleWeights).Select((w, i) => Criterion($"c{i}", w)),
            bands ?? Bands, governance ?? Governance, points ? pointBands ?? Points : null, policy, status);

    internal static CriterionScore[] Scores(EvaluationProfile profile, params int?[] values) =>
        profile.ActiveCriteria.Select((c, i) => new CriterionScore(c.CriterionId, values[i], values[i] is null ? $"Clarify {c.CriterionId}" : null)).ToArray();
}
