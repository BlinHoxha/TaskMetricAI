using System.Collections.Immutable;

namespace TaskMetricAI.Evaluation;

public sealed record ValidationError(string Code, string Path, string Message);

public sealed class EvaluationValidationException : ArgumentException
{
    public ImmutableArray<ValidationError> Errors { get; }
    public EvaluationValidationException(IEnumerable<ValidationError> errors)
        : this(errors.ToImmutableArray()) { }
    private EvaluationValidationException(ImmutableArray<ValidationError> errors)
        : base(string.Join("; ", errors.Select(e => $"{e.Code} ({e.Path}): {e.Message}"))) => Errors = errors;
}

public static class ProfileValidator
{
    public static ImmutableArray<ValidationError> Validate(EvaluationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var errors = ImmutableArray.CreateBuilder<ValidationError>();
        void Error(string code, string path, string message) => errors.Add(new(code, path, message));
        if (string.IsNullOrWhiteSpace(profile.TenantId)) Error("profile.tenant", "TenantId", "Required.");
        if (string.IsNullOrWhiteSpace(profile.ProfileId)) Error("profile.id", "ProfileId", "Required.");
        if (profile.Version < 1) Error("profile.version", "Version", "Must be positive.");
        if (string.IsNullOrWhiteSpace(profile.Name)) Error("profile.name", "Name", "Required.");
        if (profile.Description is null) Error("profile.description", "Description", "Cannot be null.");
        if (!Enum.IsDefined(profile.Scope.Type) || string.IsNullOrWhiteSpace(profile.Scope.Id))
            Error("profile.scope", "Scope", "Valid type and nonempty scope ID required.");
        if (!Enum.IsDefined(profile.Status)) Error("profile.status", "Status", "Unknown status.");
        if (string.IsNullOrWhiteSpace(profile.Governance.CreatedBy) ||
            string.IsNullOrWhiteSpace(profile.Governance.ChangeReason) || profile.Governance.CreatedAt == default)
            Error("profile.governance", "Governance", "Creator, creation time and change reason required.");
        if ((profile.Governance.ApprovedBy is null) != (profile.Governance.ApprovedAt is null) ||
            (profile.Governance.ApprovedBy is not null && string.IsNullOrWhiteSpace(profile.Governance.ApprovedBy)))
            Error("profile.approval", "Governance", "Approval actor/time must be supplied together.");
        var places = profile.Policy.PercentageDecimalPlaces;
        if (places is < 0 or > 4) Error("policy.precision", "Policy", "Percentage precision must be 0–4.");
        if (profile.Policy.ScoringVersion != "wsm-v1" || profile.Policy.SensitivityVersion != "relative-10-v1")
            Error("policy.version", "Policy", "Unsupported scoring or sensitivity policy version.");
        if (profile.Criteria.IsEmpty) Error("criteria.empty", "Criteria", "At least one criterion required.");
        if (profile.ActiveCriteria.IsEmpty) Error("criteria.active", "Criteria", "At least one enabled metric required.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        decimal total = 0;
        bool validRange = true;
        foreach (var criterion in profile.Criteria)
        {
            var path = $"Criteria[{criterion.CriterionId}]";
            if (string.IsNullOrWhiteSpace(criterion.CriterionId) || !ids.Add(criterion.CriterionId))
                Error("criterion.id", path, "IDs must be nonempty and unique (ordinal).");
            if (string.IsNullOrWhiteSpace(criterion.Label) || string.IsNullOrWhiteSpace(criterion.Definition))
                Error("criterion.description", path, "Label and definition required.");
            if (criterion.Weight is < 0 or > 1)
            {
                validRange = false;
                Error("weight.range", path, "Weight must be within [0,1].");
            }
            else if (criterion.IsEnabled) total += criterion.Weight;
            if (places is >= 0 and <= 4 && decimal.Round(criterion.Weight, places + 2) != criterion.Weight)
                Error("weight.precision", path, "Weight exceeds configured percentage precision.");
            if (!MetricRules.ValidAnchors(criterion.Anchors))
                Error("criterion.anchors", path, "Five nonempty criterion-specific score anchors required.");
            if (criterion.SourceMetric is { } source && (source.TenantId != profile.TenantId ||
                source.MetricId != criterion.CriterionId || source.Version < 1))
                Error("criterion.source", path, "Catalog reference must match tenant/criterion and have a positive version.");
        }
        if (!validRange || total != 1m) Error("weights.total", "Criteria", "Enabled weights must sum exactly to 1; no normalization.");
        ValidateBands(profile.ComplexityBands.Select(b => (b.BandId, b.Interval)), "ComplexityBands", errors);
        if (profile.ComplexityBands.Any(b => string.IsNullOrWhiteSpace(b.Name)))
            Error("band.name", "ComplexityBands", "Names required.");
        if (profile.StoryPointBands is { } points)
        {
            ValidateBands(points.Select(b => (b.BandId, b.Interval)), "StoryPointBands", errors);
            if (points.Any(b => b.Points < 1)) Error("points.range", "StoryPointBands", "Points must be positive integers.");
        }
        return errors.ToImmutable();
    }

    private static void ValidateBands(IEnumerable<(string Id, ScoreInterval Interval)> source, string path,
        ImmutableArray<ValidationError>.Builder errors)
    {
        void Error(string code, string message) => errors.Add(new(code, path, message));
        var bands = source.ToArray();
        if (bands.Length == 0) { Error("bands.empty", "Enabled bands cannot be empty."); return; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        ScoreInterval? previous = null;
        foreach (var (id, interval) in bands)
        {
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id)) Error("band.id", "IDs must be nonempty and unique.");
            if (interval is null) { Error("band.interval", "Interval required."); continue; }
            if (interval.Lower < 1 || interval.Upper > 5 || interval.Lower >= interval.Upper)
                Error("band.range", "Positive-width intervals must lie within [1,5].");
            if (previous is not null && (previous.Upper != interval.Lower || previous.IncludeUpper == interval.IncludeLower))
                Error("bands.coverage", "Ordered bands must meet without gaps or overlap, including endpoints.");
            previous = interval;
        }
        if (bands[0].Interval is not { Lower: 1m, IncludeLower: true } ||
            bands[^1].Interval is not { Upper: 5m, IncludeUpper: true })
            Error("bands.endpoints", "Bands must include endpoints 1 and 5.");
    }
}
