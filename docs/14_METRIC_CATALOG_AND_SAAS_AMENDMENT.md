# Metric-agnostic MCDA and multi-tenant SaaS amendment

Date: 28 October 2026
Status: Approved owner architectural requirement; M0/M1 implementation only

## Normative direction

TaskMetric AI is a multi-tenant SaaS product for independent companies, teams and Jira projects. This amendment supersedes fixed-eight-metric wording in earlier documents. The eight original criteria are an optional standard starter template; neither their identities nor their count belong in the evaluator. The mathematical scale remains integer 1–5 or unknown under wsm-v1; metric-agnostic does not change the approved score scale.

## Metric Catalog and profiles

- A tenant-scoped immutable Metric Catalog contains reusable standard or custom Metric Definitions: stable metric ID, definition version, label, description, and all five score anchors. IDs are unique within a catalog, not globally across companies. Catalog snapshots contain one selected version per ID; older definition versions remain available through historical snapshots/persistence later.
- The standard template explicitly creates tenant-owned copies of eight definitions. It is opt-in, has no automatic activation, and does not constrain custom profiles. Editing definitions creates new immutable versions; changing a standard copy does not affect other companies or existing profiles.
- Profile criteria are embedded immutable metric-content snapshots plus optional catalog-definition provenance, weight and IsEnabled. Direct inline definitions remain supported as self-contained custom metrics; they are not automatically catalog entries. Catalog provenance includes tenant/metric ID/version and must match the owning profile tenant and criterion ID. Profiles never dereference a live catalog while scoring.
- Authorized administrators later add/remove/disable metrics, edit descriptions/rubrics, choose weights and bands/points, manage multiple versions/profiles and assign them to organization/team/Jira-project scopes. These capabilities are modeled in M1 but management operations/APIs/UI are M2+.

## Strict rules

At least one enabled metric. Unique IDs across all entries, including disabled ones. Every definition requires a valid rubric. Every stored weight is within [0,1] and approved precision; enabled weights alone total exactly 1. Disabled weights may retain their previous allocation but contribute nothing; removal/disable does not redistribute any weights. Admins must explicitly rebalance before constructing a valid next profile. Enabled zero-weight metrics are distinct from disabled metrics: they are assessed and displayed, and unknowns do not block definitive scoring.

Require exactly one score or explicit unknown per enabled criterion. Reject disabled, removed, unknown, duplicate or omitted score IDs. Positive-weight unknowns suppress definitive score/class/points and produce bounds. Complexity and optional point intervals retain full [1,5] validation. No automatic normalization.

## Versions, snapshots and tenant boundary

Tenant ID is mandatory on catalogs, definitions, profiles, profile references and evaluation snapshots. Canonical profile content v2 hashes tenant, enabled state and metric-definition provenance as well as embedded definitions, rubrics, weights, bands and policy versions. Lifecycle/audit metadata remains outside content identity. A pure next-version operation creates Draft version N+1 without changing the original. Concurrent version allocation/activation uniqueness is M2 persistence work.

An immutable evaluation snapshot owns its exact tenant-scoped profile snapshot, submitted accepted scores/questions and deterministic result. Catalog edits, disabling/removal or later profile versions never rewrite it. Tenant/reference mismatches are rejected by pure contracts; actual authentication, query isolation and cross-tenant access enforcement require M2+ services and security tests. Do not claim those controls already exist.

No reinterpretation of old profile-content-v1 hashes. V2 is an explicit contract change; persisted legacy snapshots, if introduced externally, must preserve their schema/content/result and use a compatible reader. M1 has no database migration and does not delete old reports.

## Dynamic AI contract (M4)

Generate request criteria and response validation from the resolved snapshot's enabled criteria, including tenant/profile ID/version/hash, definitions and rubrics. Accept only that exact ID set with scores 1–5 or unknown. Do not parse model-supplied weights, definitions, removals, activation or assignment as authority. No metric-management tools are exposed during evaluation. Future workflow uses the same deterministic score-set validator. No hardcoded eight-field prompt/schema.

## Scope and architecture

M0: reconcile docs/ADRs and record this amendment. M1: pure catalog/template/profile/criterion/evaluation snapshot contracts, validation, metric-agnostic scoring/sensitivity and unit tests. Keep all in the existing dependency-free Evaluation assembly; no catalog microservice, database, new host or external service.

M2+: persistence, delegated authorization, tenant management/isolation, catalog/profile CRUD/version history and assignment APIs. M4: dynamic AI consumption. M5: authorized management/review surface as needed. Existing manually deployed Hello World Forge starter is setup evidence only and is untouched by this M1 amendment.

## Acceptance evidence

Test custom-only profiles and counts other than eight; explicit standard template; add/remove/disable/re-enable via next versions; exact enabled-weight validation without repair; duplicate/invalid metric definitions; catalog tenant mismatch; stale/cross-tenant profile references; catalog edits leaving old profiles/evaluations unchanged; immutable score/result/profile capture; enabled-only sensitivity and assessment IDs; configured bands/points; all prior vectors, unknowns, determinism and dependency-boundary tests. Record results in docs/15_METRIC_CATALOG_ALIGNMENT_REVIEW.md and stop for owner review before M2.
