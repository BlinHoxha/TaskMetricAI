using System.Collections.Immutable;

namespace TaskMetricAI.Evaluation;

/// <summary>Complete in-memory historical snapshot; no database, lifecycle or access control is implied.</summary>
public sealed class EvaluationSnapshot
{
    public string TenantId { get; }
    public string EvaluationId { get; }
    public EvaluationProfile Profile { get; }
    public ImmutableArray<CriterionScore> Scores { get; }
    public EvaluationResult Result { get; }

    public EvaluationSnapshot(string tenantId, string evaluationId, EvaluationProfile profile,
        ProfileReference expectedProfile, IEnumerable<CriterionScore> scores)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(tenantId) || tenantId != profile.TenantId)
            throw new EvaluationValidationException([new("snapshot.tenant", "TenantId", "Snapshot must match profile tenant.")]);
        if (string.IsNullOrWhiteSpace(evaluationId))
            throw new EvaluationValidationException([new("snapshot.id", "EvaluationId", "Required.")]);
        var accepted = McdaEvaluator.ValidateInputs(profile, expectedProfile, scores);
        TenantId = tenantId;
        EvaluationId = evaluationId;
        Profile = profile;
        Scores = accepted;
        Result = McdaEvaluator.Evaluate(profile, expectedProfile, accepted);
    }
}
