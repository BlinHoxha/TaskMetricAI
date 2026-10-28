# TaskMetric AI — Product Requirements Document

## Approved metric-agnostic SaaS direction (28 October 2026)

Support independent companies, teams and Jira projects through tenant-isolated metric catalogs, profile configurations and evaluation history. Standard eight-metric content is opt-in; administrators may create custom-only profiles, remove/disable metrics and edit definitions/rubrics/weights/bands/points. Preserve exact immutable historical versions. Docs/14 and ADR 0004 govern M0/M1 contracts; management/authorization/persistence/UI remain later milestones.
Version: 1.1 | Status: Proposed | Target: Jira Cloud MVP

## Vision
Approved amendment: authorized administrators/evaluation managers own criteria, weights, rubric anchors, complexity bands and optional team point mappings. AI proposes scores only. Every report identifies the exact immutable profile ID/version/hash. Example weights are illustrative, never automatically activated. See docs/12 and ADR 0001.

A Jira issue panel that evaluates an issue against evidence from its mapped GitHub repository and provides transparent, reviewable MCDA complexity scores, suggested story points, risks, and technical subtasks. The evaluation is advisory, not a performance assessment.

## Users and jobs
- Developer: understand scope, likely affected code, and edge cases before implementation.
- Tech lead: compare estimates and challenge unsupported assumptions.
- Product owner: identify missing acceptance criteria and dependencies.
- Engineering manager: inspect team-calibrated estimates without treating points as person-hours.

## Core workflow
1. Admin connects a Jira Cloud site and authorizes read access to selected GitHub repositories.
2. Admin maps Jira project/component to repository and default branch; issue-level overrides require authorization.
3. User opens issue panel and requests evaluation.
4. Backend fetches permitted issue fields, linked issues and relevant repository snapshot.
5. Analyzer identifies potential code impact with citations; AI proposes scores and missing scenarios.
6. Deterministic MCDA engine validates profile and computes results.
7. User reviews evidence, adjusts scores and approves publishing a summary to Jira.

## MVP in scope
Jira Cloud issue panel; GitHub read-only integration; C#/.NET code analysis; manual and AI-assisted scoring; weighted sum; uncertainty and source references; evaluation history; approved Jira comment publishing; basic repository mapping; cancellation and error states.

## Out of scope
Automatic code modifications, autonomous issue status transitions, mandatory story point field updates, multi-repository semantic graph, transcript analysis, sprint forecasting, employee productivity scoring, Azure DevOps, enterprise-scale monorepo analysis.

## Nonfunctional requirements
- No source code executed during analysis by default; bounded read-only processing.
- Tenant isolation, encrypted secrets, scoped permissions, audit log, retention controls.
- Reproducible scores for identical inputs and profile version.
- Every asserted code finding references repository, commit SHA, path and line range where possible.
- AI uncertainty clearly labeled; never claim exact implementation files for unbuilt functionality.

## Success criteria
- Evaluate a sample Jira issue with mapped GitHub .NET repository end-to-end.
- All computed weighted scores independently reproducible in tests.
- Reviewer can distinguish verified facts from inferred impact.
- No external Jira writes before explicit user approval.
- Invalid/ambiguous mappings and inaccessible repositories fail safely.

## Commercial direction
Internal pilot first, then multi-tenant SaaS; keep MCDA engine provider-independent so it can be integrated into DevScope AI later.

## v1.1 UX and agentic scope decision
The MVP is **agent-first with a minimal Jira Cloud embedded interface**. Jira is the primary user workspace; no standalone React SPA, independent user login, or frontend hosting. Implement a Forge UI Kit issue panel with Evaluate, status, criterion evidence, manual override, and explicit Publish actions. A richer Forge Custom UI (React/TypeScript) is deferred until the UI Kit is demonstrably insufficient. The backend is an orchestrated workflow, not a multi-agent platform. User approval is required for all Jira writes.
