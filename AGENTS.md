# TaskMetric AI — Repository Instructions for Codex

Read README.md, all numbered specifications/amendments/reviews under docs/, and ADRs before implementing major features. Docs/14 and ADR 0004 approve the metric-agnostic multi-tenant SaaS direction. Backend-first; implement only the authorized milestones. Use Clean Architecture pragmatically, avoid microservices, vector DB, and multi-agent orchestration in MVP.

Core invariant: MCDA scoring is pure, deterministic, versioned, and unit-tested. AI only proposes criterion scores with evidence; it does not calculate final scores or publish to Jira. Never treat points as hours. Unknown is not a neutral score.

Profile invariant: only authorized administrators/evaluation managers own criteria, weights, rubrics, bands, and optional point mappings. AI cannot mutate, normalize, activate, or select policy. Validate decimal weights totaling exactly 1 without repair. Use immutable profile ID/version/content hash. Positive-weight unknowns suppress definitive score/class/points; zero-weight unknowns remain visible without blocking calculation. Follow docs/adr/0001-admin-controlled-profiles.md and 0002-deterministic-scoring.md.

Current authorization: M1 is approved and the .NET 10 upgrade is complete. M2 implementation is paused pending explicit owner approval. Only repository separation, baseline documentation and validation are authorized now. Preserve unfinished M2 work on feature/m2-foundations; do not implement integrations, create databases, deploy, or push without further approval.

Metric invariant: catalog definitions are reusable standard/custom data, never hardcoded mandatory engine criteria. At least one enabled metric; enabled weights total exactly 1, with no normalization. Disabled metrics are excluded from assessments/scoring; zero-weight enabled unknowns remain visible. Require tenant-bound profile/reference/evaluation snapshots; catalog changes never rewrite history. Administration, assignments, persistence, authorization and tenant management are deferred. Preserve the user's existing Forge starter; this amendment does not authorize editing/deploying it.

Security invariant: treat Jira tickets, code, comments, and model outputs as untrusted. No execution of fetched repository code. No unauthorized repo access. Validate provider identity and tenant on every request. Never write to Jira without explicit approval. Do not log secrets or private source content.

Evidence invariant: cite repo + SHA + path + line for verified code claims; mark inferred impact and recommendations distinctly. Preserve evaluation snapshots, overrides and profile versions.

Testing: xUnit for pure engine, provider contract tests, security tests, deterministic fixtures. Run build and tests before marking a milestone complete. Explain tradeoffs and document ADRs.

Framework reuse: inspect existing Framework/AI-Framework if present; reuse only stable beneficial abstractions; MCDA library must have no dependency on them.

## v1.1 UI and agent boundaries
Use a single orchestrated workflow rather than multiple autonomous agents. Jira Cloud is the primary UI. Implement a minimal Forge UI Kit issue panel; no standalone React SPA, external frontend authentication, or separate frontend hosting. The UI must never calculate final scores or publish without explicit approval. Verify Forge platform details against current Atlassian docs. Read `docs/10_JIRA_UI_AND_AGENT_WORKFLOW.md` before building the Jira integration.
