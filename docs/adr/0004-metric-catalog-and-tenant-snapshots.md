# ADR 0004: Metric Catalog and tenant-scoped immutable snapshots

Date: 28 October 2026
Status: Accepted under owner-approved SaaS amendment (docs/14)

The evaluator already handles arbitrary criteria, but reusable definitions, tenant identity and disabled membership were missing. Add pure contracts to the existing Evaluation library rather than creating a separate service or infrastructure layer.

MetricCatalog is a tenant-owned immutable current-definition set. MetricDefinition has tenant, stable ID, version, standard/custom kind, description and five anchors. StandardMetricTemplate is an explicitly invoked data factory; McdaEvaluator and SensitivityAnalyzer never reference it. A profile embeds criterion contents so live catalog lookup is unnecessary. Optional MetricReference provenance identifies tenant/metric/version; inline custom criteria remain valid for caller-defined policy. Catalog-backed snapshots must match the profile tenant/criterion identity.

IsEnabled defines profile membership. Validate all metric identities/rubrics/weight ranges, but require at least one enabled criterion and enabled weight sum exactly 1. Ignore disabled entries in score-set validation, contributions, unknowns and sensitivity. Scores for disabled metrics are rejected, not ignored. Enabled zero-weight unknowns remain visible. No automatic rebalancing when membership changes.

TenantId is explicit and required, not inferred from scope or caller credentials. References and canonical content include it. This catches accidental composition mismatches; it does not implement authorization, tenant management or storage isolation. Tenant-aware repositories, caches, composite keys and scope authorization remain mandatory M2 work.

Canonical schema becomes profile-content-v2 (supersedes v1 for new snapshots), preserving fixed field ordering, invariant decimals and SHA-256 while adding tenant, enabled state and catalog provenance. Never silently rehash stored v1 content. Historical reports are retained; no persistence/deserializer migration is in scope.

EvaluationProfile.CreateNextVersion creates an immutable Draft with checked version increment and explicitly supplied criteria/bands/optional mappings, requiring a new change reason/governance. EvaluationSnapshot captures exact profile, accepted criteria scores/questions and result, all read-only. No authorization or atomic version allocation is implied. Catalog/profile updates cannot mutate old results.

One shared ScoreAnchors validation rule is used for catalog and inline profile definitions. Metric-specific meaning is reviewed by humans; the library checks structural completeness. No custom scoring executable code, per-metric strategy plugin, new scoring scale, AI-driven policy mutation or automatic profile activation is introduced.

AI later consumes only enabled definitions from a tenant/profile/version/hash-bound snapshot. Score validation rejects extras/missing/disabled IDs. Schema validation must reject policy-mutation fields; the model has no management capabilities. No AI provider or prompt execution is implemented in M1.
