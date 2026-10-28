# Jira UI & Agent Workflow Specification

## Dynamic profile amendment

Future panel rows and AI proposals use the selected snapshot's enabled metrics, never a fixed eight-row list. Show tenant/profile identity, labels, rubric, weights and evidence for arbitrary metrics. Removed/disabled metrics are not assessment inputs; zero-weight enabled metrics remain visible. Administration, assignment APIs and tenant-management implementation remain later milestones. The existing Hello World starter is setup only; M1 amendment does not change/deploy it. See docs/14.
Version: 1.1 | Status: Approved architectural direction

## Goal and non-goals
TaskMetric AI is a Jira Cloud-embedded, agent-first evaluator. The MVP does **not** require a standalone frontend application. Users stay inside Jira. No autonomous multi-agent platform, dashboard, or independent sign-in.

## UI choice
Use Atlassian Forge **UI Kit** for the issue panel. Provide: Evaluate, evaluation progress/error state, weighted criteria breakdown, evidence links and confidence/unknown indicators, edit-score-with-reason, and Review/Publish confirmation. Use Jira's existing navigation and identity. If charts or complex controls are essential later, evaluate Forge Custom UI (React + TypeScript) in a separate ADR.

## Bounded evaluation workflow
1. Authorized user requests evaluation for a Jira issue.
2. Backend verifies tenant, user, issue permissions, repo mapping and repository access.
3. Read issue fields and pin a GitHub repository snapshot SHA.
4. Inspect relevant C# symbols, dependencies and tests without executing fetched code.
5. AI proposes structured criterion scores, missing information, edge cases and evidence references.
6. Validate schema and evidence; unresolved scores remain unknown.
7. Pure C# MCDA engine computes score, classification and optional candidate story points.
8. Save a draft evaluation with profile version and snapshot references.
9. UI shows results, allows authorized override with reason, and requests explicit publish approval.
10. Backend performs idempotent Jira publication and records audit data.

## UI states
Not started; queued/running; completed; partial evidence; needs clarification; failed; approved/published. Show partial and unknown values explicitly. Never silently default missing scores to 3/5.

## Responsibility boundaries
Approved profile amendment: display resolved profile name/scope/ID/version/hash and configured weights read-only. Reviewers override criterion scores with reasons, never weights. AI cannot mutate or activate policy. Configuration APIs/minimal admin surface are M2+; no administration UI in M1. Positive-weight unknowns show bounds with no definitive score/class/points. See docs/12.

- Forge UI Kit: trigger, view, edit, approve; never holds provider secrets or scoring rules.
- .NET application service: orchestration, validation, authorization, lifecycle and publishing gate.
- GitHub/Roslyn: read-only evidence collection with SHA/path/line references.
- LLM adapter: proposals only, no authoritative score, no Jira writes.
- MCDA library: deterministic scoring and sensitivity calculations.
- Persistence: audit, versions, evidence, overrides, idempotency.

## Security and verification
Validate Jira tenant and actor with a supported Forge-to-remote pattern. Confirm current Forge modules, scopes, egress, remote authentication, limits and UI Kit component availability against official Atlassian documentation during implementation. Treat issue text and source comments as prompt-injection surfaces. Use least-privilege read-only GitHub credentials. Require explicit approval for any Jira write.

## Acceptance criteria
- No separately hosted frontend is needed for MVP.
- The same inputs and scoring profile produce the same MCDA result.
- UI cannot override backend authorization or forge scoring values.
- Unknown evidence is clearly displayed.
- No Jira publication occurs without an approved, auditable action.
- Failed or timed-out analysis is recoverable without duplicate publishing.
