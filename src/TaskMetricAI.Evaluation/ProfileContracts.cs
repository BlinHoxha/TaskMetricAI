using System.Collections.Immutable;

namespace TaskMetricAI.Evaluation;

public enum ScopeType { Organization, Team, JiraProject }
public enum ProfileStatus { Draft, Validated, Active, Archived }

public sealed record ProfileScope(ScopeType Type, string Id);
public sealed record ProfileReference(string TenantId, string ProfileId, int Version, string ContentHash);
public sealed record ProfileGovernance(
    string CreatedBy, DateTimeOffset CreatedAt, string ChangeReason,
    string? ApprovedBy = null, DateTimeOffset? ApprovedAt = null);

/// <summary>Supported mathematical policies; no authorization or activation is implied.</summary>
public sealed record ScoringPolicy(
    int PercentageDecimalPlaces = 2,
    string ScoringVersion = "wsm-v1",
    string SensitivityVersion = "relative-10-v1");

public sealed record ScoreAnchors(string One, string Two, string Three, string Four, string Five);
public sealed record CriterionDefinition(
    string CriterionId, string Label, string Definition, decimal Weight, ScoreAnchors Anchors,
    bool IsEnabled = true, MetricReference? SourceMetric = null)
{
    /// <summary>Copy a catalog version into a profile; scoring never queries a live catalog.</summary>
    public static CriterionDefinition FromMetric(MetricDefinition metric, decimal weight, bool isEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(metric);
        return new(metric.MetricId, metric.Label, metric.Description, weight, metric.Anchors, isEnabled, metric.Reference);
    }
}

public sealed record ScoreInterval(decimal Lower, decimal Upper, bool IncludeLower = true, bool IncludeUpper = false)
{
    public bool Contains(decimal score) =>
        (score > Lower || (IncludeLower && score == Lower)) &&
        (score < Upper || (IncludeUpper && score == Upper));
}

public sealed record ComplexityBand(string BandId, string Name, ScoreInterval Interval);
public sealed record StoryPointBand(string BandId, ScoreInterval Interval, int Points, bool RecommendSplit = false);

/// <summary>
/// Immutable, validated content snapshot. Lifecycle transitions, authorization and scope resolution
/// belong to the consuming application. Constructing a snapshot never activates a profile.
/// </summary>
public sealed class EvaluationProfile
{
    public string TenantId { get; }
    public string ProfileId { get; }
    public int Version { get; }
    public string Name { get; }
    public string Description { get; }
    public ProfileScope Scope { get; }
    public ProfileStatus Status { get; }
    public ProfileGovernance Governance { get; }
    public ScoringPolicy Policy { get; }
    public ImmutableArray<CriterionDefinition> Criteria { get; }
    public ImmutableArray<CriterionDefinition> ActiveCriteria { get; }
    public ImmutableArray<ComplexityBand> ComplexityBands { get; }
    public ImmutableArray<StoryPointBand>? StoryPointBands { get; }
    public string CanonicalContent { get; }
    public string ContentHash { get; }
    public ProfileReference Reference => new(TenantId, ProfileId, Version, ContentHash);

    public EvaluationProfile(
        string tenantId, string profileId, int version, string name, string description, ProfileScope scope,
        IEnumerable<CriterionDefinition> criteria, IEnumerable<ComplexityBand> complexityBands,
        ProfileGovernance governance, IEnumerable<StoryPointBand>? storyPointBands = null,
        ScoringPolicy? policy = null, ProfileStatus status = ProfileStatus.Draft)
    {
        TenantId = tenantId;
        ProfileId = profileId;
        Version = version;
        Name = name;
        Description = description;
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        Governance = governance ?? throw new ArgumentNullException(nameof(governance));
        Policy = policy ?? new();
        Status = status;
        Criteria = Copy(criteria, nameof(criteria));
        ActiveCriteria = Criteria.Where(c => c.IsEnabled).ToImmutableArray();
        ComplexityBands = Copy(complexityBands, nameof(complexityBands));
        StoryPointBands = storyPointBands is null ? null : Copy(storyPointBands, nameof(storyPointBands));
        var errors = ProfileValidator.Validate(this);
        if (!errors.IsEmpty) throw new EvaluationValidationException(errors);
        CanonicalContent = ProfileCanonicalizer.Serialize(this);
        ContentHash = ProfileCanonicalizer.Hash(CanonicalContent);
    }

    /// <summary>Pure content revision; does not authorize edits or allocate a version transactionally.</summary>
    public EvaluationProfile CreateNextVersion(IEnumerable<CriterionDefinition> criteria,
        IEnumerable<ComplexityBand> complexityBands, IEnumerable<StoryPointBand>? storyPointBands,
        ProfileGovernance governance, string? name = null, string? description = null, ScoringPolicy? policy = null) =>
        new(TenantId, ProfileId, checked(Version + 1), name ?? Name, description ?? Description, Scope,
            criteria, complexityBands, governance, storyPointBands, policy ?? Policy, ProfileStatus.Draft);

    private static ImmutableArray<T> Copy<T>(IEnumerable<T> values, string name) where T : class
    {
        ArgumentNullException.ThrowIfNull(values, name);
        var result = values.ToImmutableArray();
        if (result.Any(value => value is null)) throw new ArgumentException("Null collection item.", name);
        return result;
    }
}
