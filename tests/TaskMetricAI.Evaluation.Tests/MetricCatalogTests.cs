using System.Collections;
using TaskMetricAI.Evaluation;

namespace TaskMetricAI.Evaluation.Tests;

public sealed class MetricCatalogTests
{
    private static MetricDefinition Custom(string tenant, string id = "review-scope", int version = 1) =>
        new(tenant, id, version, MetricKind.Custom, "Review scope", "Scope of specialist review",
            new("Local review", "One specialist", "Several reviewers", "Cross-team review", "Cross-company review"));

    [Fact]
    public void Catalog_supports_reusable_custom_metrics_and_tenant_owned_standard_copies()
    {
        var starter = StandardMetricTemplate.CreateCatalog("company-a");
        Assert.Equal(8, starter.Definitions.Length);
        Assert.All(starter.Definitions, d => { Assert.Equal(MetricKind.Standard, d.Kind); Assert.Equal("company-a", d.TenantId); });
        var catalog = new MetricCatalog("company-a", starter.Definitions.Append(Custom("company-a")));
        Assert.Equal(9, catalog.Definitions.Length);
        Assert.Equal(MetricKind.Custom, catalog.GetRequired("review-scope").Kind);
        var a = Profiles.Create(tenantId: "company-a", criteria: [CriterionDefinition.FromMetric(catalog.GetRequired("review-scope"), 1m)]);
        Assert.Equal("company-a", a.TenantId);
        Assert.Throws<EvaluationValidationException>(() => Profiles.Create(criteria: [CriterionDefinition.FromMetric(catalog.GetRequired("review-scope"), 1m)]));
    }

    [Fact]
    public void Custom_only_profile_needs_no_standard_metric_and_can_reuse_one_definition()
    {
        var definition = Custom("company-a");
        var catalog = new MetricCatalog("company-a", [definition]);
        var p1 = Profiles.Create(tenantId: "company-a", criteria: [CriterionDefinition.FromMetric(catalog.GetRequired("review-scope"), 1)]);
        var p2 = Profiles.Create(tenantId: "company-a", id: "another-profile", criteria: [CriterionDefinition.FromMetric(definition, 1)]);
        Assert.Equal(4m, McdaEvaluator.Evaluate(p1, Profiles.Scores(p1, 4)).WeightedScore);
        Assert.Equal(definition.Reference, p1.Criteria[0].SourceMetric);
        Assert.Equal(p1.Criteria[0].SourceMetric, p2.Criteria[0].SourceMetric);
        Assert.NotEqual(p1.Reference, p2.Reference);
    }

    [Fact]
    public void Standard_template_preserves_documented_vector_without_becoming_engine_policy()
    {
        var standard = StandardMetricTemplate.CreateCatalog("fixture-tenant");
        var p = Profiles.Create(criteria: standard.Definitions.Select((d, i) => CriterionDefinition.FromMetric(d, Profiles.ExampleWeights[i])).ToArray());
        Assert.Equal(3.8m, McdaEvaluator.Evaluate(p, Profiles.Scores(p, 4, 3, 4, 3, 5, 3, 5, 2)).WeightedScore);
        Assert.Equal(8, standard.Definitions.Select(d => d.MetricId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(standard.Definitions, d => Assert.False(string.IsNullOrWhiteSpace(d.Anchors.Five)));
        var custom = Profiles.Create(criteria: [Profiles.Criterion("entirely-custom", 1)]);
        Assert.Equal(2m, McdaEvaluator.Evaluate(custom, Profiles.Scores(custom, 2)).WeightedScore);
    }

    [Fact]
    public void Catalog_revisions_and_new_selections_do_not_change_old_profiles_or_other_companies()
    {
        var a = StandardMetricTemplate.CreateCatalog("company-a");
        var b = StandardMetricTemplate.CreateCatalog("company-b");
        var original = a.Definitions[0];
        var profile = Profiles.Create(tenantId: "company-a", criteria: [CriterionDefinition.FromMetric(original, 1)]);
        var before = profile.CanonicalContent;
        var revised = original.CreateNextVersion("Custom label", "Changed definition", original.Anchors with { Five = "Company A custom anchor" });
        var nextCatalog = new MetricCatalog("company-a", a.Definitions.Select(d => d.MetricId == original.MetricId ? revised : d));
        Assert.Equal(2, nextCatalog.GetRequired(original.MetricId).Version);
        Assert.Equal(1, a.GetRequired(original.MetricId).Version);
        Assert.Equal(original.Anchors.Five, b.GetRequired(original.MetricId).Anchors.Five);
        Assert.Equal(before, profile.CanonicalContent);
        Assert.Equal(original.Label, profile.Criteria[0].Label);
        Assert.Equal(1, profile.Criteria[0].SourceMetric!.Version);
    }

    [Fact]
    public void Catalog_rejects_cross_tenant_entries_duplicate_ids_and_invalid_definitions()
    {
        Assert.Throws<EvaluationValidationException>(() => new MetricCatalog("a", [Custom("b")]));
        Assert.Throws<EvaluationValidationException>(() => new MetricCatalog("a", [Custom("a"), Custom("a", version: 2)]));
        Assert.Throws<EvaluationValidationException>(() => new MetricCatalog("", []));
        Assert.Throws<EvaluationValidationException>(() => new MetricCatalog("a", [null!]));
        Assert.Throws<EvaluationValidationException>(() => new MetricDefinition("a", "", 1, MetricKind.Custom, "x", "x", Custom("a").Anchors));
        Assert.Throws<EvaluationValidationException>(() => new MetricDefinition("a", "x", 0, MetricKind.Custom, "x", "x", Custom("a").Anchors));
        Assert.Throws<EvaluationValidationException>(() => new MetricDefinition("a", "x", 1, (MetricKind)99, "x", "x", Custom("a").Anchors));
        Assert.Throws<EvaluationValidationException>(() => new MetricDefinition("a", "x", 1, MetricKind.Custom, "", "x", Custom("a").Anchors));
        Assert.Throws<EvaluationValidationException>(() => new MetricDefinition("a", "x", 1, MetricKind.Custom, "x", "x", null!));
        Assert.Throws<EvaluationValidationException>(() => new MetricDefinition("a", "x", 1, MetricKind.Custom, "x", "x", Custom("a").Anchors with { Two = "" }));
        Assert.Throws<KeyNotFoundException>(() => new MetricCatalog("a", []).GetRequired("missing"));
    }

    [Fact]
    public void Catalog_collection_and_definition_properties_are_immutable()
    {
        var input = new[] { Custom("a") };
        var c = new MetricCatalog("a", input);
        input[0] = Custom("a", "different");
        Assert.Equal("review-scope", c.Definitions[0].MetricId);
        Assert.Throws<NotSupportedException>(() => ((IList)c.Definitions)[0] = Custom("a", "different"));
        Assert.All(typeof(MetricDefinition).GetProperties(), p => Assert.Null(p.SetMethod));
        Assert.All(typeof(MetricCatalog).GetProperties(), p => Assert.Null(p.SetMethod));
    }
}
