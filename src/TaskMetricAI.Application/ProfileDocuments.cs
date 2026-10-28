using System.Text.Json;
using System.Text.Json.Serialization;
using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Application;

public sealed record ProfileInput(string Name, string Description, ProfileScope Scope,
    CriterionDefinition[] Criteria, ComplexityBand[] ComplexityBands, StoryPointBand[]? StoryPointBands,
    ScoringPolicy Policy, string Reason);
public sealed record MetricInput(string Label, string Description, ScoreAnchors Anchors, string Reason);
public sealed record ProfileDocument(string Schema, string TenantId, string ProfileId, int Version,
    string Name, string Description, ProfileScope Scope, CriterionDefinition[] Criteria,
    ComplexityBand[] ComplexityBands, StoryPointBand[]? StoryPointBands, ScoringPolicy Policy,
    ProfileGovernance Governance, string ContentHash)
{
    public static ProfileDocument From(EvaluationProfile p) => new("profile-document-v2", p.TenantId, p.ProfileId,
        p.Version, p.Name, p.Description, p.Scope, p.Criteria.ToArray(), p.ComplexityBands.ToArray(),
        p.StoryPointBands?.ToArray(), p.Policy, p.Governance, p.ContentHash);

    public EvaluationProfile Restore(ProfileStatus status)
    {
        Require.That(Schema == "profile-document-v2", 409, "unsupported_snapshot_schema");
        var p = new EvaluationProfile(TenantId, ProfileId, Version, Name, Description, Scope, Criteria,
            ComplexityBands, Governance, StoryPointBands, Policy, status);
        Require.That(p.ContentHash == ContentHash, 409, "snapshot_integrity_failed");
        return p;
    }
}
public static class Documents
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Json);
    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Json)
        ?? throw new OperationException(409, "invalid_stored_document");
    public static EvaluationProfile Restore(ProfileVersion row)
    {
        var p = Read<ProfileDocument>(row.SnapshotJson).Restore(row.Status);
        Require.That(p.TenantId == row.TenantId && p.ProfileId == row.ProfileId && p.Version == row.Version &&
            p.ContentHash == row.ContentHash && p.Scope.Type == row.ScopeType && p.Scope.Id == row.ScopeId,
            409, "snapshot_integrity_failed");
        return p;
    }
}
