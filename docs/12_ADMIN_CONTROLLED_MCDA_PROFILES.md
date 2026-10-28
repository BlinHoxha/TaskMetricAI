# TaskMetric AI — Administrator-Controlled MCDA Profiles

**Status:** Approved architectural requirement by owner on 27 October 2026  
**Baseline:** TaskMetric AI v1.1 and `11_ARCHITECTURAL_ASSESSMENT_AND_IMPLEMENTATION_PLAN.md` (27 October 2026)  
**Purpose:** Make criterion importance, score interpretation, complexity bands, and story-point mappings explicitly owned by authorized humans, never autonomously set by AI.

## 1. Decision and boundaries

Approved 28 October 2026 amendment: this is a multi-tenant SaaS with a configurable Metric Catalog. Docs/14 and ADR 0004 are normative for metric definitions, tenant identity, enabled membership and snapshots. The eight metrics below are an optional standard starter template, never a mandatory metric set. Authorized administrators may add/remove/disable metrics, edit descriptions/rubrics and manage multiple scoped profiles; administration implementation remains deferred.

An authorized System Administrator or Evaluation Manager defines and approves **evaluation profiles**. A profile specifies the criteria and their weight distribution (total exactly 100%), rubric anchors, complexity bands, and optional team-specific story-point mappings. The AI may propose criterion **scores** and evidence, but must never choose, alter, normalize, activate, or publish profile weights. The deterministic C# MCDA engine evaluates using the exact immutable profile version selected for the Jira project/team.

The application must distinguish:
- **Weights**: policy chosen by an authorized human (how important each criterion is).
- **Criterion scores**: 1–5 assessments, optionally proposed by AI and reviewable by humans (how challenging this task is on each criterion).
- **Complexity bands**: administrator-defined interpretation of the weighted result.
- **Story points**: optional team-calibrated mapping; not equivalent to hours.
- **Priority**: separate business decision; never silently mixed into complexity.

## 2. Roles and permissions

| Role | Authorized operations |
| --- | --- |
| System Administrator | Define organization scopes, delegate profile-management rights, create/validate/activate/archive profiles. |
| Evaluation Manager | Manage profiles only within explicitly delegated team/Jira-project scopes. |
| Reviewer | Inspect evidence, adjust **criterion scores** with reason, approve a specific evaluation revision for publishing if permitted. |
| Developer | Request evaluation, inspect accessible results, suggest feedback. |
| AI Assessor | Propose criterion scores and explanations; read only the approved immutable profile. No profile mutation or Jira publishing authority. |

Authorization is enforced server-side; hiding controls in Forge is insufficient. Scope checks apply to every operation, and changes must be auditable.

## 3. Profile data contract

A versioned immutable profile contains:

- `TenantId`, `ProfileId`, `Version`, `Name`, `Description`, `ScopeType` (`Organization`, `Team`, `JiraProject`), `ScopeId`.
- `Status`: `Draft`, `Validated`, `Active`, `Archived`.
- Ordered metric snapshots: stable `CriterionId`, display label, definition, five rubric anchors, `IsEnabled`, weight as `decimal` in `[0,1]`, and optional tenant/metric/version catalog provenance. At least one enabled metric. Catalog edits never mutate embedded snapshots.
- Complexity bands: stable names, lower/upper bounds, inclusive/exclusive flags, contiguous coverage of `[1,5]`.
- Optional story-point mapping: contiguous non-overlapping intervals covering `[1,5]` if enabled; split recommendation may be configured.
- Scoring policy and sensitivity policy versions; creator, approver, timestamps, reason for change; immutable content hash.

A profile becomes immutable when activated **or used by any evaluation**. Editing an existing active/used version is prohibited; changes produce a new version. Evaluation snapshots reference exact profile ID/version/hash and preserve the weights used.

## 4. Weight distribution and validation

For all stored criteria, weights satisfy `0 <= w_i <= 1`; **enabled** weights sum exactly to 1 using decimal arithmetic. At least one metric must be enabled. Disabled metrics may retain prior weights but are excluded from the total and evaluation; they are not unknown/zero-weight active metrics. Zero weights on enabled metrics are allowed. No hidden renormalization, inferred default replacement, or AI-generated weights. Removing/disabling a metric requires explicit human rebalancing if the active total changes.

Admin configuration should show each weight in percent, running total, remaining/excess allocation, and a validation error until the total equals 100%. An optional explicit **redistribute** action may assist editing, but it must preview the proposed values and require administrator confirmation; the engine itself never silently changes them. Decimal percentage precision must be defined by profile policy and validated consistently.

Example *illustrative* default profile (not mandatory):

| Criterion | Weight |
| --- | ---: |
| Implementation complexity | 20% |
| Codebase impact | 15% |
| Integration complexity | 15% |
| Data complexity | 10% |
| Testing complexity | 15% |
| Requirements uncertainty | 10% |
| Security and reliability | 10% |
| Deployment complexity | 5% |
| **Total** | **100%** |

Managers may select different weights for different teams/projects. The profile used must be visible on every evaluation report.

## 5. Rubrics and mapping

Define **criterion-specific** anchored meanings for all five possible scores, rather than generic 'low to high' labels. E.g. integration complexity: `1` no integration changes; `2` single existing integration with known contract; `3` moderate changes across an existing integration; `4` multiple integrations or significant failure/retry behavior; `5` cross-system coordination with substantial reliability/contract risk. These are proposed examples, not verified facts about a specific task.

Suggested configurable complexity bands for the initial profile:
- `[1.00,1.50)` Very Low
- `[1.50,2.00)` Low
- `[2.00,3.00)` Moderate
- `[3.00,4.00)` High
- `[4.00,5.00]` Very High

Optional team story-point mapping, kept **separate** from classification:
- `[1,1.5)` → 1; `[1.5,2)` → 2; `[2,2.5)` → 3; `[2.5,3.25)` → 5; `[3.25,4)` → 8; `[4,5]` → 13 (consider splitting).

All bands must cover the full allowed domain with no gaps or overlap. Evaluate with unrounded decimal results; round only for display. No points if mapping is disabled.

## 6. Evaluation mathematics and unknowns

For fully known positive-weight criteria: `S = sum(w_i * s_i)`; normalized index `N = 100 * (S - 1) / 4`. Return criterion contributions, profile identity/version, and classification.

For an unknown score with **positive** weight: report provisional lower/upper bounds by substituting 1 and 5 respectively; do not produce a definitive score, class, or point suggestion. Preserve missing-information questions. An unknown **zero-weight** criterion remains visible but does not prevent definitive scoring. Do not reweight known criteria.

Confidence is independent of complexity, and AI may not convert low evidence quality into an arbitrarily high complexity score. Story points are not automatically converted to hours.

## 7. Profile lifecycle and precedence

`Draft → Validated → Active → Archived`. Validation is not authorization to activate. Record actor, scope, timestamp, and change reason. At most one active profile per **effective scope**; preserve prior versions for historical evaluation reproducibility.

Proposed resolution order: explicit authorized Jira-project assignment, then team assignment, then organization default. If a Jira issue resolves to multiple equally ranked profiles, stop and require authorized human selection; never choose based on AI preference. Record resolved profile and scope in the evaluation.

For a new version: clone an existing version into a draft, edit, validate, review differences, activate explicitly. Existing evaluations remain attached to their original version. Re-evaluation under a new version creates a **new evaluation attempt** and does not rewrite history.

## 8. UI requirements (deferred)

The Jira Forge UI Kit MVP remains an **evaluation and review** interface, not a full administrative dashboard. For the pilot, use authenticated configuration APIs or a minimal admin surface when M2/M5 requires it. Do not add a standalone React SPA.

A later authorized configuration interface should show: tenant/profile name/scope/version/status, reusable standard/custom metric catalog, arbitrary enabled/disabled criterion selection with live active-weight total, rubric editor, complexity-band editor, optional points mapping, validate/preview, activate, compare versions, and audit history. A regular developer sees a read-only summary of the profile applied to the ticket.

## 9. Audit and security

Persist append-only events for profile creation, draft changes, validation, activation, archiving, and evaluation overrides, including actor, scope, timestamp, reason, before/after or diff, and version/hash. Prevent unauthorized cross-tenant or cross-project profile access. Preserve exact evaluation snapshot. Never publish Jira updates without separate explicit evaluation-revision approval.

## 10. Implementation alignment

**M0:** Record an ADR approving admin-controlled weights, profile lifecycle, scope precedence, unknown handling, and precision policy. Update `04_MCDA_SPECIFICATION.md`, `02_FUNCTIONAL_REQUIREMENTS.md`, `03_TECHNICAL_ARCHITECTURE.md`, `06_DATABASE_SCHEMA.md`, `07_IMPLEMENTATION_PLAN.md`, `08_SECURITY_AND_INTEGRATION.md`, `09_CODEX_MASTER_PROMPT.md`, `10_JIRA_UI_AND_AGENT_WORKFLOW.md`, and `AGENTS.md` as needed. Keep this document as the normative policy addendum.

**M1:** Implement pure immutable profile/value types, deterministic validation, weighted scoring, provisional unknown bounds, configurable bands and point mapping, and sensitivity calculation. **No** database, HTTP, Jira, Forge, AI, or shared-framework dependencies. Use .NET 10 LTS if SDK is available.

**M2 onward:** Implement profile persistence, authorization, scope resolution, and APIs as required; add Jira-facing review/publishing later. AI may only assess task criteria using the resolved profile, never change profile policy.

## 11. M1 acceptance tests

The SaaS amendment also requires custom-only/arbitrary-count metrics, standard-template copying, catalog revision isolation, enabled/disabled membership, exact enabled-weight totals, cross-tenant reference rejection and complete immutable EvaluationSnapshot tests. See docs/14 and current results in docs/15_METRIC_CATALOG_ALIGNMENT_REVIEW.md. AI execution, authorization and persistence are not M1 acceptance claims.

1. Weights total exactly 1; reject 0.99, 1.01, negative weights, duplicates, and silently normalized input.
2. Admin-defined weight sets produce different deterministic outputs for identical criterion scores; no hardcoded production weight distribution.
3. All score anchors are present for each configured criterion; score values are integers 1–5 or explicit unknown.
4. Full, gap-free classification and point intervals, including 1.495 and boundary values; classification before rounding.
5. Positive-weight unknowns produce correct bounds and no definitive points; zero-weight unknowns do not block calculation.
6. Evaluation output contains exact profile ID, version, and stable identity/hash where appropriate.
7. Input/profile objects and results are immutable; repeated and concurrent evaluations are deterministic.
8. Sensitivity analysis has documented fixed precision, deterministic residual allocation, and degenerate-case handling.
9. Test vectors: all 1 → score 1/index 0; all 3 → score 3/index 50; all 5 → score 5/index 100; existing documented vector → score 3.80/index 70.
10. The production evaluation assembly has no dependencies on Jira, GitHub, AI, HTTP, EF Core, or shared frameworks.

## 12. Non-goals

No AI-driven weight optimization, automatic profile activation, dynamic weight changes per task, automated hours-from-points conversion, standalone frontend, full administrative UI in M1, or production Jira publishing in M1.

## 13. Open implementation details to record in ADR

- Decimal precision and exact deterministic sensitivity residual allocation.
- Whether an activated but unused profile can ever be retired without cloning (recommended: archive only).
- Team/project profile resolution for Jira issues with multiple components.
- Profile content hash canonicalization rules and immutable snapshot serialization.

Codex must propose concrete resolutions, record them in ADRs, and ask for approval if a choice changes product policy rather than merely implementation detail.

## 14. Recorded implementation resolutions

See [ADR 0001](adr/0001-admin-controlled-profiles.md) for ownership, lifecycle and scope precedence; [ADR 0002](adr/0002-deterministic-scoring.md) for precision, canonical hashing, unknowns and sensitivity; and [ADR 0003](adr/0003-m0-m1-foundations.md) for milestone/runtime boundaries. These implement this approved addendum without adding administration or provider integrations to M1.
