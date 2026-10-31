# TaskMetric AI — Repository Instructions for Codex

Checkpoint authorization (31 October 2026): the owner approved a final cumulative M2 review and one descriptive local commit on feature/m2-foundations if safe. See docs/25_M2_FEATURE_CHECKPOINT.md for the reviewed inventory and boundary. Stop after that commit; no push, merge, rebase, branch switch, master change or additional implementation is authorized. Earlier no-commit statements below record previous review gates and do not override this explicit owner instruction.

Current evidence (31 October 2026): bounded review corrections in docs/24; current state in docs/context/IMPLEMENTATION_STATUS.md and SESSION_HANDOFF.md. Startup requires schema 3/grants/READ COMMITTED. Populated-v2 scope registration requires explicit operator approval/manifest, never inference. Locally validated; stop for review. No further implementation/commit/push/merge authority is implied.

Latest bounded increment: owner-approved effective-profile resolution/local entitlements/grant revocation, ADR 0008 and docs/23. Implemented and locally validated; stop for owner review. Older pending-resolution statements are historical. Preserve master and all uncommitted work; no commit/push/merge authorization.

Session handoff: read `docs/context/SESSION_HANDOFF.md` first for current evidence, limitations and approval boundaries; its companion status, decision index and fresh-session prompt preserve context. M2 remains local and uncommitted; stop for owner review. This reference authorizes no further implementation or Git operations.

Read README.md, all numbered specifications/amendments/reviews under docs/, and ADRs before implementing major features. Docs/14 and ADR 0004 approve the metric-agnostic multi-tenant SaaS direction. Backend-first; implement only the authorized milestones. Use Clean Architecture pragmatically, avoid microservices, vector DB, and multi-agent orchestration in MVP.

Core invariant: MCDA scoring is pure, deterministic, versioned, and unit-tested. AI only proposes criterion scores with evidence; it does not calculate final scores or publish to Jira. Never treat points as hours. Unknown is not a neutral score.

Profile invariant: only authorized administrators/evaluation managers own criteria, weights, rubrics, bands, and optional point mappings. AI cannot mutate, normalize, activate, or select policy. Validate decimal weights totaling exactly 1 without repair. Use immutable profile ID/version/content hash. Positive-weight unknowns suppress definitive score/class/points; zero-weight unknowns remain visible without blocking calculation. Follow docs/adr/0001-admin-controlled-profiles.md and 0002-deterministic-scoring.md.

Current authorization (30 October 2026): the bounded M2 security-hardening increment on feature/m2-foundations includes verified tenant transactions, existing least-privileged runtime role integration, relationship/lifecycle/audit hardening, a configuration-only ASP.NET Core HTTP boundary and mandatory PostgreSQL security tests. Use only taskmetricai_dev/taskmetricai_test and credentials outside Git. Do not recreate local roles/databases or change passwords. Preserve master and the pure M1 engine. No provider integrations, AI, Roslyn, advanced UI, commit, push or merge. Stop for review after validation. See docs/20 and docs/21 plus ADR 0007.

Metric invariant: catalog definitions are reusable standard/custom data, never hardcoded mandatory engine criteria. At least one enabled metric; enabled weights total exactly 1, with no normalization. Disabled metrics are excluded from assessments/scoring; zero-weight enabled unknowns remain visible. Require tenant-bound profile/reference/evaluation snapshots; catalog changes never rewrite history. Administration, assignments, persistence and authorization belong to M2, not the pure M1 engine; consult current status for what is implemented. Preserve the user's existing Forge starter; this amendment does not authorize editing/deploying it.

Security invariant: treat Jira tickets, code, comments, and model outputs as untrusted. No execution of fetched repository code. No unauthorized repo access. Validate provider identity and tenant on every request. Never write to Jira without explicit approval. Do not log secrets or private source content.

Evidence invariant: cite repo + SHA + path + line for verified code claims; mark inferred impact and recommendations distinctly. Preserve evaluation snapshots, overrides and profile versions.

Testing: xUnit for pure engine, provider contract tests, security tests, deterministic fixtures. Run build and tests before marking a milestone complete. Explain tradeoffs and document ADRs.

Framework reuse: inspect existing Framework/AI-Framework if present; reuse only stable beneficial abstractions; MCDA library must have no dependency on them.

## v1.1 UI and agent boundaries
Use a single orchestrated workflow rather than multiple autonomous agents. Jira Cloud is the primary UI. Implement a minimal Forge UI Kit issue panel; no standalone React SPA, external frontend authentication, or separate frontend hosting. The UI must never calculate final scores or publish without explicit approval. Verify Forge platform details against current Atlassian docs. Read `docs/10_JIRA_UI_AND_AGENT_WORKFLOW.md` before building the Jira integration.

## Session handoff

Before further M2 work, read [docs/context/SESSION_HANDOFF.md](docs/context/SESSION_HANDOFF.md), which links the decision index, implementation/validation status and fresh-session prompt. Preserve master and all uncommitted feature work. The handoff is documentation-only and ends at owner review; it grants no implementation, commit, push, merge or branch-switch authorization.
