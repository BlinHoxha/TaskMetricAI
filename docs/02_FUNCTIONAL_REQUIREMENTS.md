# Functional Requirements and Use Cases

## Approved Metric Catalog amendment (normative: docs/14)

- FR-METRIC-01: Reusable tenant-owned standard/custom definitions with stable IDs, immutable versions, descriptions and five anchors; optional standard template.
- FR-METRIC-02: Authorized administrators add/remove/disable profile metrics and edit rubrics, weights, classifications and points; active/used edits create new versions. Multiple profiles and organization/team/Jira-project assignments are supported by later application services.
- FR-METRIC-03: At least one enabled metric; all IDs unique; enabled weights exactly 1 with no normalization. Disabled entries differ from zero-weight enabled entries.
- FR-METRIC-04: AI requests and schema are generated from enabled snapshot criteria only. Reject disabled/extra/missing IDs and any model authority to mutate policy. No fixed metric list.
- FR-METRIC-05: Tenant identity, exact profile content/version/hash, submitted scores and result are immutable evaluation contracts. Authentication, persistent tenant isolation and management APIs remain M2+.

## Approved administrator-profile requirements (normative: docs/12)
- FR-PROFILE-01: System Administrators and explicitly delegated Evaluation Managers configure profiles within authorized tenant/team/project scopes. Reviewers edit scores only; AI has no policy mutation/activation authority.
- FR-PROFILE-02: Validate exact decimal total 1 and configured precision, all five criterion-specific anchors, and contiguous separate complexity/optional point intervals. Never silently normalize.
- FR-PROFILE-03: Preserve profile ID/version/hash and exact weights on evaluations. Activated or used content is immutable; editing clones a new draft. Audit lifecycle/overrides append-only from M2.
- FR-PROFILE-04: Explicit project assignment precedes team then organization. Equal-ranked matches require authorized human selection, never AI choice.
- FR-PROFILE-05: Positive-weight unknowns return bounds with no definitive score/class/points. Zero-weight unknowns remain visible without blocking calculation. Disabled point mapping yields no points.
- FR-PROFILE-06: Authorization, lifecycle, persistence and APIs are M2+; M1 provides pure content contracts and validation only. Validation is not authorization to activate.


## FR-01 Jira issue retrieval
Fetch issue key, summary, description (Atlassian Document Format), issue type, configured custom fields, acceptance criteria when present, links and authorized comments. Preserve field provenance. Handle missing fields and permission errors.

## FR-02 Repository resolution
Resolve repository via explicit issue mapping, then project/component mapping. Show ambiguous matches for human selection. Pin analysis to a commit SHA; do not silently analyze a moving branch after evaluation starts.

## FR-03 Repository inspection
Enumerate solution/projects, controllers, endpoints, DTOs, application services, domain models, EF Core contexts, migrations, tests, dependency references. Use Roslyn where practical; avoid claims of complete call graph coverage.

## FR-04 AI evidence assessment
Return structured JSON for each criterion: score 1–5 or unknown, rationale, supporting issue/code references, confidence descriptor, missing information. Validate JSON strictly; untrusted issue/code text is data, never instructions.

## FR-05 MCDA computation
Validate active profile, criterion weights, scoring scale and completeness; calculate weighted score and classification deterministically. Support manual override with actor, timestamp and reason.

## FR-06 Estimate suggestions
Map complexity to candidate story points only under configured team profile; default mapping is provisional. Do not equate points with hours. Without sufficient evidence, show 'requires refinement' rather than a fabricated estimate.

## FR-07 Edge-case and subtask suggestions
Propose negative cases, concurrency, authorization, error handling, migrations, tests and deployment considerations when relevant. Each suggestion marked verified, inferred or recommendation.

## FR-08 Jira review and publishing
Show preview and approval; publish a concise comment or dedicated app storage record. Updating story points requires a separate explicit permission and confirmation; avoid duplicate comments using evaluation ID.

## FR-09 History and audit
Persist profile version, issue snapshot hash, repository SHA, AI model/prompt version, raw validated criterion proposal, final score, overrides, and publication events.

## FR-10 Failure handling
Gracefully handle rate limits, GitHub/Jira outages, deleted branches, missing access, token expiry, oversized repositories, AI timeouts and uncertain matching.

## Use case UC-01 Evaluate ticket
Actor: developer. Precondition: issue visible and mapped repo authorized. Main: open issue → evaluate → inspect evidence → adjust → save draft → approve publication. Alternatives: ambiguous repo, no matching code, incomplete acceptance criteria, denied access.

## Use case UC-02 Compare assessments
Actor: tech lead. Review initial vs overridden scores, profile versions and rationale; differences must remain auditable.

## Use case UC-03 Configure scoring
Actor: admin. Define criteria and weights, validate sum=100%, version profile and activate for Jira project/team. Previous evaluations retain old profile.

## Acceptance examples
- Given duplicate evaluation requests for same issue/SHA/profile, system can return cached result without duplicate Jira publication.
- Given no supporting code evidence, output explicitly states inferred impact and lowers confidence.
- Given weights not summing to 100%, profile cannot be activated.
- Given unauthorized GitHub repository, analysis stops without leaking source snippets.

## v1.1 Jira-embedded interaction requirements
- FR-UI-01: Show an Evaluate action in the Jira Cloud issue panel for authorized users.
- FR-UI-02: Show evaluation lifecycle states (not started, running, partial, needs clarification, complete, failed) and retry where safe.
- FR-UI-03: Display criterion scores, weights, source references, unknowns, and verified/inferred labels.
- FR-UI-04: Allow manual score overrides with required rationale; recalculate in the deterministic backend.
- FR-UI-05: Separate review from publishing; Publish requires an explicit confirmation and authorization.
- FR-UI-06: Render errors without leaking private repository content or credentials.
- FR-UI-07: Keep the MVP entirely inside Jira; no independent web dashboard or frontend authentication.
