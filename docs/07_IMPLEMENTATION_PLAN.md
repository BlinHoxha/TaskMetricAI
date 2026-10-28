# Backend-First Implementation Roadmap

## Milestone 0 — Repository and security foundations
Preserve/reconcile docs and approved admin-profile addendum; create solution/test foundations, SDK/package pins and CI; record threat model, profile ownership, lifecycle, scope precedence, hashing and precision ADRs. PostgreSQL/configuration and provider authentication implementation move to M2. Acceptance: local Release build/tests pass, CI workflow is prepared, secrets/build outputs excluded. A hosted CI run requires a Git repository/remote.

## Milestone 1 — Pure MCDA engine
Approved SaaS extension (docs/14): add pure tenant-scoped Metric Catalog and versioned standard/custom definitions, opt-in standard eight-metric template, explicit enabled/disabled profile entries, profile next-version copying and immutable complete evaluation snapshots. Engine/assessment-set validation operates only on enabled metric IDs, with exact active weight total. Tests demonstrate arbitrary counts, custom-only profiles, disabled-vs-zero distinction, cross-tenant composition rejection and historical reproducibility. Preserve the existing Forge starter; no changes to integrations/UI in this amendment.

Implement pure immutable configurable profiles, five anchors per criterion, exact decimal validation, weighted contributions, unknown bounds, independent bands/optional points, stable content hash and sensitivity scenarios. Acceptance: deterministic xUnit/property-style tests cover all addendum cases and architecture independence. Never normalize invalid configured weights; normalization applies only to sensitivity scenarios. No production default profile activation, database, HTTP, shared framework or provider references. Stop for owner review before M2.

## Milestone 2 — Jira and GitHub read connectors
Connect sandbox Jira Cloud and test GitHub repo, mapping and pinned SHA. Acceptance: read issue/repo with least privilege, ambiguous mapping safe failure, rate limit handling.
Add profile persistence, append-only lifecycle/audit, delegated scope authorization, explicit activation/archiving, effective-scope resolution and authenticated configuration APIs. Resolve Jira-project assignment then team then organization; ambiguity requires human selection. Configure PostgreSQL here. Validate Forge identity before exposing provider reads.
Add tenant management and storage/query/cache isolation, Metric Catalog persistence/version history and scoped metric/profile administration and assignment APIs. Add/remove/disable metrics and rubric edits create new active/used profile versions, never historical rewrites. Standard template is optional and independently customizable per tenant. No catalog microservice.

## Milestone 3 — .NET analyzer
Roslyn metadata and evidence references, relevant code shortlist, test discovery, bounded analysis. Acceptance: fixture projects analyzed with verified file/line citations and no source execution.

## Milestone 4 — AI-assisted assessment
Generate AI criteria/schema from the selected tenant/profile/version/hash snapshot's enabled metrics and rubrics. Validate exact score ID set and reject policy-mutation fields. No fixed eight-field prompt and no AI catalog-management tools.

Structured JSON response, schema validation, evidence citation checking, unknowns and inferred-vs-verified distinctions. Acceptance: prompt-injection fixture does not cause unauthorized action; unsupported claims rejected or flagged.

## Milestone 5 — Jira issue panel and review
Forge panel invokes backend, displays state and criterion breakdown, supports override and explicit publish. Acceptance: no writes without approval; idempotent publication; proper tenant identity checks.

## Milestone 6 — Pilot and calibration
Evaluate 20–30 diverse real or synthetic tickets, review with tech leads, compare scoring consistency, document known failure modes and refine weights. Do not use as individual productivity assessment.

## Definition of Done
Automated tests appropriate to the milestone; documentation and ADR updated; secrets not logged. Evidence/access-control tests and manual Jira smoke tests apply when their integrations exist, not to M0/M1. Reviewer approval before Jira writes.

## Suggested API acceptance scenarios
200/202 evaluation creation, invalid profile 422, unauthorized repo 403, issue missing 404, provider rate limit retry/backoff, malformed AI JSON graceful failure, duplicate publication idempotent, stale SHA recorded, unsupported repo reported as partial analysis.

## v1.1 implementation refinement
The first four milestones remain backend-first. **Milestone 5 is Forge UI Kit**, not a standalone React SPA: create a small issue panel, connect it securely to the backend, display evaluation state/evidence, allow review/overrides, and require explicit publish approval. Defer Forge Custom UI and any external dashboard until a documented user need. Avoid multiple autonomous agents, vector databases, microservices and agent-to-agent protocols in MVP. Validate Forge current platform capabilities before coding.
