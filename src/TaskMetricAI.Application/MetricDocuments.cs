using TaskMetricAI.Domain;
using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Application;

public sealed record MetricDocument(string Schema, string TenantId, string MetricId, int Version,
    MetricKind Kind, string Label, string Description, ScoreAnchors Anchors)
{
    public static MetricDocument From(MetricDefinition metric) => new("metric-document-v1", metric.TenantId,
        metric.MetricId, metric.Version, metric.Kind, metric.Label, metric.Description, metric.Anchors);
    public MetricDefinition Restore()
    {
        Require.That(Schema == "metric-document-v1", 409, "unsupported_metric_schema");
        return new(TenantId, MetricId, Version, Kind, Label, Description, Anchors);
    }
}

public static class MetricDocuments
{
    public static MetricDefinition Restore(MetricVersion row)
    {
        var metric = Documents.Read<MetricDocument>(row.DefinitionJson).Restore();
        Require.That(metric.TenantId == row.TenantId && metric.MetricId == row.MetricId && metric.Version == row.Version,
            409, "metric_integrity_failed");
        return metric;
    }
}
