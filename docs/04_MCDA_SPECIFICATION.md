# MCDA Mathematical and Evaluation Specification

## Purpose and boundaries
Approved SaaS amendment (docs/14, ADR 0004): reusable tenant-scoped Metric Catalog, arbitrary standard/custom criteria, enabled/disabled profile membership, tenant-bound immutable profile/evaluation snapshots. The evaluator has no knowledge of standard metric IDs or count. Validate at least one enabled metric and sum enabled weights exactly 1; never repair allocations. Only enabled criteria are assessed/scored, including enabled zero-weight criteria. Disabled scores are rejected. All metric IDs/rubrics/ranges remain validated even when disabled. Catalog definition edits and new profile versions never rewrite old evaluations. The existing eight-metric table is an optional starter template.

WSM measures relative implementation complexity, not business priority, developer quality or elapsed calendar time. Business prioritization is a separate optional model. AI suggests evidence-based criterion values; deterministic C# computes results.

## Illustrative criteria (administrator-configurable, not an active production default)
| Criterion | Weight | Examples |
|---|---:|---|
| Implementation complexity | 20% | branching, algorithms, business rules |
| Codebase impact | 15% | layers, modules, consumers |
| Integration complexity | 15% | external systems, events, API contracts |
| Data complexity | 10% | migrations, transactions, consistency |
| Testing complexity | 15% | negative paths, integration and regression |
| Uncertainty | 10% | unclear requirements, unknown dependencies |
| Security and reliability | 10% | permissions, concurrency, resilience |
| Deployment complexity | 5% | rollout, configuration, infrastructure |

For enabled criteria i, each score s_i ∈ {1,2,3,4,5}; all stored weights are in [0,1] and sum(enabled w_i)=1 exactly. Weighted score S = Σ over enabled criteria (w_i × s_i). Disabled entries are excluded. Normalized complexity index N = 100×(S−1)/4. N is NOT probability or percent complete. Round only for presentation; retain full decimal for classification.

## Anchored rubric
1: isolated, known pattern, low uncertainty; 2: modest localized change; 3: multiple code paths or moderate integration; 4: broad/high-risk impact; 5: cross-cutting, complex or insufficiently bounded. Each criterion should have its own documented examples; do not use rubric labels as substitutes for evidence.

## Unknown and confidence
Unknown criterion is null, not 3. Positive-weight unknowns require provisional score/index bounds by substituting 1 and 5, with no definitive score, class or points. Zero-weight unknowns remain visible but do not block definitive scoring. Preserve missing questions and never reweight known scores. Confidence is separate evidence coverage/quality classification (low/medium/high), not a statistically calibrated probability unless validated against outcomes. Never fabricate '72% confidence'.

## Provisional team-specific story point mapping
When explicitly enabled in an administrator-approved team profile: [1,1.5) → 1; [1.5,2) → 2; [2,2.5) → 3; [2.5,3.25) → 5; [3.25,4) → 8; [4,5] → 13 and recommend splitting. These are illustrative, not mandatory. Complexity bands are independently configurable and both interval sets must cover [1,5] without gaps/overlap. Classify unrounded values, including 1.495. Disabled mapping produces no points.

## Effort hours
Optional separate activity-based estimate: implementation + tests + integration + review + release. Present ranges and assumptions; never derive hours by multiplying story points by a universal constant.

## Sensitivity analysis
Perturb one enabled weight by ±10% relative only in explicitly labeled hypothetical scenarios; never mutate the approved profile. Redistribute other enabled weights proportionally using 12 fractional weight digits and exact largest-remainder allocation, ties by ordinal criterion ID. Disabled metrics are excluded. Zero-weight and sole-positive-weight targets are not applicable; a target exceeding 1 is not applicable. See ADR 0002 for exact arithmetic and residual rules. Incomplete results remain provisional. Task ranking across profiles is deferred beyond M1. Do not mix priority and complexity criteria.

## Validation and test vectors
- All scores=1 → S=1, N=0.
- All scores=5 → S=5, N=100.
- All scores=3 → S=3, N=50.
- Example scores [4,3,4,3,5,3,5,2] and weights above → S=3.80 (verify with unit test).
- Invalid weights, out-of-range scores, duplicate criterion IDs and stale profile versions rejected.
- Same profile version + same scores → same deterministic output.

## Governance
`12_ADMIN_CONTROLLED_MCDA_PROFILES.md` is the normative policy addendum. Only authorized administrators/evaluation managers configure weights, criteria, five criterion-specific anchors, bands and mappings. No autonomous AI policy changes or hidden normalization. Weight precision is profile-configured (0–4 decimal percentage places); unknown policy versions are rejected. Profile snapshots are immutable, identified by ID/version/SHA-256 content hash. Lifecycle, authorization and effective-scope resolution are implemented from M2, outside the pure engine. See ADRs 0001–0003.

Store who assigned each score (AI/user), supporting evidence, profile version, rationale, override reason and outcome. Evaluate model performance by comparing recommendations with team retrospective judgments and completed-work evidence; do not infer individual productivity.

## v1.1 agent responsibility boundary
AI may extract evidence and **propose** criterion scores, including an unknown state. Only the pure C# evaluator validates weights, computes weighted results, derives classifications and candidate points. The Forge panel may show explanations and collect overrides but must not implement a competing scoring formula. Never equate story points with hours.
