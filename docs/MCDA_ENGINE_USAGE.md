# MCDA engine contracts and usage

M1 exposes only immutable scoring content, validation and arithmetic. Application authorization, profile activation, assignment precedence, persistence and publishing are intentionally implemented later. A successfully constructed snapshot is not proof of authorization or activation.

The 28 October SaaS amendment adds MetricCatalog, MetricDefinition, StandardMetricTemplate, enabled membership and EvaluationSnapshot. TenantId is required on profile construction/reference; profile canonical content is now v2. See docs/14 and ADR 0004. Historical v1 hashes are never reinterpreted.

## Illustrative usage

The following is an example of caller-supplied policy, not a default production profile. Real profiles must be approved by authorized administrators/evaluation managers. `ProfileGovernance.CreatedBy` is audit metadata supplied by the application, not an authentication claim validated by this library.

```csharp
using TaskMetricAI.Evaluation;

var profile = new EvaluationProfile(
    tenantId: "example-company",
    profileId: "example-team-policy",
    version: 1,
    name: "Illustrative integration assessment",
    description: "Example only; no automatic activation",
    scope: new ProfileScope(ScopeType.Team, "example-team"),
    criteria: new[]
    {
        new CriterionDefinition(
            "integration", "Integration", "Assess external contract changes", 1m,
            new ScoreAnchors(
                "No integration changes",
                "One existing integration with a known contract",
                "Moderate changes across an existing integration",
                "Multiple integrations or significant retry behavior",
                "Cross-system coordination with substantial contract risk"))
    },
    complexityBands: new[]
    {
        new ComplexityBand("lower", "Lower complexity", new ScoreInterval(1m, 3m)),
        new ComplexityBand("higher", "Higher complexity", new ScoreInterval(3m, 5m, true, true))
    },
    governance: new ProfileGovernance(
        "example-admin", new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero), "Initial example"));

var result = McdaEvaluator.Evaluate(
    profile,
    profile.Reference, // Exact ID, version and content hash from the selected snapshot
    new[] { new CriterionScore("integration", 3) });

// result.WeightedScore == 3m; result.NormalizedIndex == 50m
// result.Classification.BandId == "higher"; result.PointSuggestion == null
// No point mapping was enabled. No hours are calculated.

var provisional = McdaEvaluator.Evaluate(
    profile, new[] { new CriterionScore("integration", null, "Which external contract changes?") });
// RequiresRefinement; Bounds [1,5]/[0,100]; score/class/points are null.

var sensitivity = SensitivityAnalyzer.Analyze(profile, new[] { new CriterionScore("integration", 3) });
// A sole positive criterion has no proportional recipients: scenarios are NotApplicable.

var snapshot = new EvaluationSnapshot(
    profile.TenantId, "example-evaluation", profile, profile.Reference,
    new[] { new CriterionScore("integration", 3) });
// Snapshot retains exact profile content/version/hash, scores/questions and result.
```

## Reusable catalog and profile membership

```csharp
var catalog = StandardMetricTemplate.CreateCatalog("example-company");
var selected = catalog.GetRequired("integration-complexity");
var criterion = CriterionDefinition.FromMetric(selected, weight: 1m);
// Use this one selected definition in a profile; other standard metrics are optional.

var updatedMetric = selected.CreateNextVersion(
    selected.Label, "Company-specific description", selected.Anchors with { Five = "Company-specific rubric anchor" });
var nextCatalog = new MetricCatalog(catalog.TenantId,
    catalog.Definitions.Select(d => d.MetricId == selected.MetricId ? updatedMetric : d));
// Existing profiles never dereference nextCatalog; embedded contents remain unchanged.

var nextProfile = profile.CreateNextVersion(
    new[] { criterion }, profile.ComplexityBands, storyPointBands: null,
    governance: new ProfileGovernance("example-admin",
        new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero), "Select catalog metric"));
// New Draft version N+1; old profile/evaluation unchanged. No activation or persistence.
```

Use `IsEnabled = false` on a criterion copy to disable it in a new version, or omit it to remove it. Explicitly rebalance enabled weights to 1; the engine never does so. Disabled weights may retain prior values but are excluded from scoring. Enabled zero-weight metrics still require a score/unknown. Only `ActiveCriteria` are sent to future AI assessment; scores for disabled/removed IDs are rejected. SourceMetric records catalog origin/version, while the embedded content is authoritative for that profile snapshot. Profile-specific overrides must be versioned and must not be mistaken for a catalog update.

## Public API

- `EvaluationProfile`: defensive immutable copies of ordered criteria/bands, governance/status metadata, scope, policy, canonical content and hash. Constructor rejects invalid content with `EvaluationValidationException` containing structured `ValidationError` entries. Null required objects/collection items are rejected as argument errors.
- `ScoreAnchors`: five explicit nonempty anchors. `CriterionDefinition.Weight` is an administrator-configured decimal fraction; `CriterionScore.Score` is nullable integer 1–5. A fractional score is not representable.
- `ScoreInterval`: decimal endpoints and inclusivity flags. Ordered intervals must cover [1,5] with exactly one owner at shared boundaries. Null `StoryPointBands` disables points; enabled-empty is invalid.
- `ProfileReference`: exact tenant/ID/version/hash. Pass the expected reference to reject stale/different/cross-tenant content. The convenience overload uses the supplied profile's own reference; it does not resolve an assigned/active profile or authenticate a caller.
- `McdaEvaluator.Evaluate`: canonical profile-order contributions, unknown questions, score/index bounds, optional definitive score/class/points, and original profile identity. Zero-weight unknowns contribute 0 and remain visible. Positive-weight unknowns suppress definitive values.
- `SensitivityAnalyzer.Analyze`: baseline and ±10% hypothetical scenarios with weights, results and optional changed-class/point flags. No configured weights are changed. `IsStable` is null for incomplete baselines or no applicable scenarios. Skips carry explicit reasons.

All snapshots and results are immutable; record `with` expressions create separate values. No wall clock, randomness, I/O, credentials or logging is used by production scoring. Validation errors for missing assessments follow profile order; assessment errors otherwise preserve submitted order.

`MetricDefinition` and `MetricCatalog` are read-only reusable domain content. Catalogs may be empty during configuration; an evaluation profile must have at least one enabled criterion. A catalog selects one version per unique ID and rejects mixed-tenant definitions. `EvaluationSnapshot` computes and captures a valid result from its profile and accepted scores, rather than accepting a caller-forged result. These are in-memory contracts, not persistent storage or access-control enforcement.

Precision, supported policy identifiers, hash schema and sensitivity residual rules are specified in [ADR 0002](adr/0002-deterministic-scoring.md). The library validates that all five anchors are populated; semantic review of their criterion-specific meanings belongs to administrators. Lifecycle status is descriptive metadata and does not gate historical recomputation.
