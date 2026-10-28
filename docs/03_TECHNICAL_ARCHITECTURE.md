# Technical Architecture

## Approved metric-agnostic SaaS refinement

MetricCatalog, MetricDefinition, standard template, weighted/enabled criterion snapshots and immutable tenant-bound profile/evaluation snapshots are pure types in the existing Evaluation library (M1). No live catalog lookup during scoring, no mandatory metric IDs/count, and no new service. Application services later own tenant-aware catalog/profile management, delegated authorization and organization/team/project assignments; infrastructure owns persistence. AI assessment dynamically consumes enabled snapshot definitions only. See docs/14 and ADR 0004.

## Deployment topology
Approved profile amendment: resolve authorized project/team/organization assignments before assessment. Pass exact immutable profile ID/version/hash read-only to AI and to the evaluator. AI cannot choose weights, normalize, activate or change profiles. Application services own lifecycle, scope resolution, authorization, persistence and audit from M2. M1 contains only Evaluation and tests; no API/database/Forge scaffold is needed. Precision and hash rules follow ADR 0002.

Jira Cloud issue panel (Atlassian Forge UI Kit, TypeScript; no standalone SPA) → authenticated backend ASP.NET Core API → orchestration application services → Jira connector, GitHub connector, Roslyn analyzer, LLM adapter, pure MCDA library → PostgreSQL. Use a bounded background worker only when synchronous evaluation exceeds request limits.

## Projects
- TaskMetricAI.Api: endpoints, auth, validation, problem responses.
- TaskMetricAI.Application: EvaluateIssue use case, repository resolution, evidence assembly, publishing workflow.
- TaskMetricAI.Domain: IssueReference, RepositorySnapshot, Evidence, CriterionAssessment, Evaluation, Profile, Approval.
- TaskMetricAI.Evaluation: pure deterministic WSM and sensitivity calculations, no EF/HTTP/LLM dependencies.
- TaskMetricAI.Infrastructure: Jira/GitHub clients, persistence, LLM adapter, Roslyn-based inspection.
- TaskMetricAI.Jira: small Forge UI Kit issue panel, with Forge resolver/remote invocation; no standalone React SPA.
- tests: unit, integration, contract, security.

## Logical pipeline
1. Validate tenant/user and issue access.
2. Resolve repo mapping and pin commit SHA.
3. Collect minimal relevant source context with path/line references.
4. Assemble Jira requirements and evidence bundle.
5. Request structured AI criterion proposals; apply schema and citation validation.
6. Flag unknown scores and insufficient evidence; request human input when needed.
7. Compute deterministic result from accepted criterion scores.
8. Save evaluation snapshot and render review panel.
9. Publish only after explicit approval; record idempotency key.

## API endpoints (proposed)
POST /api/evaluations {jiraIssueId, repositoryOverride?} → 202 {evaluationId} or 200 completed result.
GET /api/evaluations/{id} → state, scores, evidence, errors.
POST /api/evaluations/{id}/overrides → validated criterion changes and reason.
POST /api/evaluations/{id}/publish → explicit approval and destination.
GET /api/profiles and POST /api/profiles → versioned profile configuration (admin).
POST /api/repository-mappings → authorized mapping (admin).

## Security
Validate Forge-to-backend identity and tenant binding per Atlassian-supported remote authentication design; never trust issue IDs or repository URLs alone. Encrypt provider tokens; store server-side. Enforce repo allowlists, no arbitrary network fetches, no execution of cloned source. Protect against prompt injection from ticket text, code comments and README. Apply size/time/token budgets, redact secrets before LLM requests, log no credentials. Verify current Atlassian Forge manifest/scopes and remote requirements against official documentation during implementation.

## Extensibility
Interfaces: IIssueProvider, IRepositoryProvider, ICodeAnalyzer, IAssessmentProvider, IEvaluationEngine, IPublisher. First adapters: Jira Cloud, GitHub, Roslyn, selected LLM. Later: Azure DevOps, other languages, DevScope AI.

## Framework reuse policy
Inspect existing Framework/AI-Framework APIs first. Reuse only demonstrably useful stable abstractions; never couple the pure MCDA library to either framework. Document each decision in ADRs.

## v1.1 agent-first architecture decision
Implement **one bounded evaluation orchestrator**, not independent autonomous agents: Jira issue reader → repository resolver and read-only inspector → AI evidence/criterion proposer → deterministic MCDA evaluator → persisted draft → user review/override → approved publisher. Each step has a typed input/output, timeout, failure state, and audit record. The AI cannot call publish actions or calculate authoritative MCDA results.

### Frontend boundary
Use **Atlassian Forge UI Kit** as the default issue-panel implementation. Keep UI state limited to evaluation status, score display, evidence drill-down, overrides and publish confirmation. Backend remains independently callable and deployable. Do not create a separately hosted React SPA, frontend login, design system, or dashboard. Consider Forge Custom UI only via a documented ADR when requirements exceed UI Kit capabilities.

### Integration caveat
Before implementation, verify current Atlassian Forge modules, UI Kit capabilities, remote auth/egress declarations, scopes and issue panel APIs against official docs; do not assume exact manifests or APIs from this specification.
