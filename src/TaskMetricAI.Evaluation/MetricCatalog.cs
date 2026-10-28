using System.Collections.Immutable;

namespace TaskMetricAI.Evaluation;

public enum MetricKind { Standard, Custom }
public sealed record MetricReference(string TenantId, string MetricId, int Version);

/// <summary>Reusable immutable metric content. Kind records origin, not edit permissions.</summary>
public sealed class MetricDefinition
{
    public string TenantId { get; }
    public string MetricId { get; }
    public int Version { get; }
    public MetricKind Kind { get; }
    public string Label { get; }
    public string Description { get; }
    public ScoreAnchors Anchors { get; }
    public MetricReference Reference => new(TenantId, MetricId, Version);

    public MetricDefinition(string tenantId, string metricId, int version, MetricKind kind,
        string label, string description, ScoreAnchors anchors)
    {
        var errors = ImmutableArray.CreateBuilder<ValidationError>();
        if (string.IsNullOrWhiteSpace(tenantId)) errors.Add(new("metric.tenant", "TenantId", "Required."));
        if (string.IsNullOrWhiteSpace(metricId)) errors.Add(new("metric.id", "MetricId", "Required."));
        if (version < 1) errors.Add(new("metric.version", "Version", "Must be positive."));
        if (!Enum.IsDefined(kind)) errors.Add(new("metric.kind", "Kind", "Unsupported metric kind."));
        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(description))
            errors.Add(new("metric.description", "Description", "Label and description required."));
        if (!MetricRules.ValidAnchors(anchors)) errors.Add(new("metric.anchors", "Anchors", "Five nonempty score anchors required."));
        if (errors.Count != 0) throw new EvaluationValidationException(errors);
        TenantId = tenantId;
        MetricId = metricId;
        Version = version;
        Kind = kind;
        Label = label;
        Description = description;
        Anchors = anchors;
    }

    public MetricDefinition CreateNextVersion(string label, string description, ScoreAnchors anchors) =>
        new(TenantId, MetricId, checked(Version + 1), Kind, label, description, anchors);
}

/// <summary>One selected definition per ID for a tenant. This is content, not CRUD or authorization.</summary>
public sealed class MetricCatalog
{
    public string TenantId { get; }
    public ImmutableArray<MetricDefinition> Definitions { get; }

    public MetricCatalog(string tenantId, IEnumerable<MetricDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var copy = definitions.ToImmutableArray();
        var errors = ImmutableArray.CreateBuilder<ValidationError>();
        if (string.IsNullOrWhiteSpace(tenantId)) errors.Add(new("catalog.tenant", "TenantId", "Required."));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in copy)
        {
            if (definition is null) { errors.Add(new("catalog.definition", "Definitions", "Null definition.")); continue; }
            if (definition.TenantId != tenantId) errors.Add(new("catalog.tenant", definition.MetricId, "Cross-tenant definition rejected."));
            if (!ids.Add(definition.MetricId)) errors.Add(new("catalog.duplicate", definition.MetricId, "One selected version per metric ID required."));
        }
        if (errors.Count != 0) throw new EvaluationValidationException(errors);
        TenantId = tenantId;
        Definitions = copy;
    }

    public MetricDefinition GetRequired(string metricId) =>
        Definitions.FirstOrDefault(d => d.MetricId == metricId) ?? throw new KeyNotFoundException("Metric is not in this catalog.");
}

internal static class MetricRules
{
    internal static bool ValidAnchors(ScoreAnchors? anchors) => anchors is not null &&
        !new[] { anchors.One, anchors.Two, anchors.Three, anchors.Four, anchors.Five }.Any(string.IsNullOrWhiteSpace);
}
