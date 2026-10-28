# TaskMetric AI — Architectural Assessment and Implementation Plan

Date: 27 October 2026  
Documentation baseline: TaskMetric AI v1.1  
Status: M0/M1 authorized on 27 October 2026; reconciled with approved administrator-profile addendum

## 1. Purpose and review scope

This document records the architectural assessment, gaps, reuse decisions, proposed solution structure, roadmap, and detailed Milestone 1 scope for TaskMetric AI.

The assessment reviewed the attached `TaskMetricAI_Documentation_v1.1.zip`: `AGENTS.md`, `README.md`, and all ten documents under `docs/`, including the Codex Master Prompt and Implementation Plan. It also inspected relevant documentation and source APIs in the local `Framework` and `AI-Framework` repositories.

At review time, the local TaskMetricAI repository contained an empty solution. The v1.1 documentation was read directly from the ZIP. This assessment does not imply that the package has been extracted into the repository.

The owner's instruction to assess and wait for approval takes precedence over implementation instructions contained in the documentation. No application code has been written or modified as part of this review. No application build or tests were run.

The initial review text below records findings at assessment time. The owner subsequently authorized documentation reconciliation and M0/M1 only. Approved docs/12 and ADRs 0001–0003 supersede conflicting proposals below: administrator-owned configurable policy, positive/zero-weight unknown rules, project/team/org precedence, content hashing and precision. M2 remains subject to owner review. Initial statements that no code/tests exist refer to the original assessment; actual milestone results are recorded separately.

## 2. Architectural recommendation

### Approved 28 October 2026 SaaS amendment

Docs/14 and ADR 0004 extend the baseline: tenant-owned reusable standard/custom Metric Catalog; arbitrary enabled/disabled profile metrics; administrator-defined definitions/rubrics/weights/bands/points; tenant-bound immutable profile and evaluation snapshots. Only enabled weights total 1. The standard eight metrics are optional template data, never mandatory engine inputs. AI later consumes exactly the enabled ID set from the selected snapshot and cannot change policy. M1 adds pure catalog/template/snapshot contracts and validation in the existing Evaluation assembly. M2 implements tenant management/isolation, delegated authorization, persistence, catalog/profile lifecycle and assignment APIs; M4 implements dynamic assessment. No new service/infrastructure or UI is required. The user-created Forge Hello World panel is existing setup and is preserved by this amendment.

The v1.1 architecture is a sound basis for the project. Use a modular ASP.NET Core backend, an independent deterministic MCDA library, and a minimal Atlassian Forge UI Kit interface embedded in Jira Cloud.

```text
Jira Forge UI Kit
        |
        | authenticated requests
        v
ASP.NET Core API
        |
Single evaluation orchestrator
        +-- Jira issue reader
        +-- GitHub snapshot reader
        +-- Roslyn evidence analyzer
        +-- AI criterion proposer
        +-- Pure MCDA evaluator
        +-- PostgreSQL evaluation history

Explicit user approval
        |
        v
Separate authorized Jira publisher
```

### Responsibility boundaries

- Forge triggers evaluation, displays status and evidence, collects overrides, and obtains explicit publishing approval.
- The application orchestrator owns authorization, workflow execution, validation, revisions, cancellation, and recovery.
- GitHub and Roslyn provide bounded, read-only evidence pinned to a commit SHA.
- AI proposes criterion assessments and explanations; it cannot calculate authoritative results or invoke publishing.
- The MCDA library validates scoring inputs and calculates results deterministically.
- Persistence preserves snapshots, profile versions, proposals, overrides, approvals, and publication attempts.
- Publishing is a separate application operation that rechecks authorization and the exact approved revision.

Keep one typed, bounded workflow. Do not introduce autonomous agents, microservices, vector databases, a standalone frontend, or separate frontend authentication for the MVP.

### Execution model

Use asynchronous evaluation from the first integrated workflow: persist a job, return `202 Accepted`, and let the panel poll for status. A PostgreSQL-backed job table and a hosted worker within the same deployment are sufficient initially.

Forge Remote currently permits 25 seconds for UI requests. Repository inspection and model inference make a synchronous-only workflow fragile. Each workflow step needs cancellation, time and size budgets, recorded outcomes, and recoverable failure handling.

Source: [Forge Remote essentials](https://developer.atlassian.com/platform/forge/remote/essentials/).

## 3. Missing requirements and architectural inconsistencies

| Area | Finding | Proposed resolution |
| --- | --- | --- |
| Milestone ordering | The roadmap separates foundations into M0, while the Master Prompt combines scaffolding and scoring. | Complete minimal solution/test foundations immediately before M1. Defer PostgreSQL setup until persistence is needed. |
| Story-point boundaries | Ranges such as 1.00–1.49 and 1.50–1.99 leave unrounded values such as 1.495 undefined. | Use half-open intervals: `[1,1.5)`, `[1.5,2)`, `[2,2.5)`, `[2.5,3.25)`, `[3.25,4)`, `[4,5]`. Classify before presentation rounding. |
| Complexity classification | Classifications are required, but their names and thresholds are not defined independently from story points. | Version explicit complexity bands in the profile and keep optional team point mappings separate. |
| Unknown scores | Null handling exists, but completeness and publication rules are incomplete. | Positive-weight unknowns produce a provisional range and “requires refinement,” without a definitive score or point suggestion. Define zero-weight handling explicitly. |
| Confidence | Low/medium/high is required without an aggregation policy. | Define a versioned evidence-quality policy using coverage, evidence validity, analysis limitations, and unresolved information. Keep confidence separate from complexity. |
| Scoring rubric | Structural 1–5 anchors must be provided for every selected metric. | Administrator-reviewed metric-specific rubrics, with arbitrary standard/custom catalog entries. Eight metrics are only an optional starter template. Missing evidence must not become score 5. |
| Lifecycle | Database and UI state lists differ; cancellation and publication failure are missing. | Separate execution state, evidence completeness, review revision, and publication status. Partial evidence may accompany completed execution. |
| Approval | Actor and timestamp do not identify exactly what was approved. | Bind approval to evaluation revision, destination, and rendered publication content hash. Overrides invalidate approval. Recheck authorization at publication. |
| Override history | A mutable assessment row cannot preserve successive overrides. | Store append-only revisions or override events with actor, timestamp, reason, and previous/new values. |
| Re-evaluation | Returning an existing evaluation to analysis risks overwriting history. | Create a new attempt or revision while preserving prior snapshots and results. |
| Cache identity | The proposed key omits inputs that can change results. | Include tenant/installation scope, repository identity, SHA, issue snapshot, immutable profile version, analyzer/prompt/model configuration, and engine version. Reauthorize cache reads. |
| Repository mappings | Component priority, multiple components, and explicit issue mappings are underspecified. | Define precedence, tie behavior, authorized overrides, and persistence for issue mappings. Require human selection for ambiguity. |
| Tenant isolation | “Every row tenant-scoped” is not reflected in all listed child entities. | Enforce tenant-aware queries and relationships, including composite constraints where appropriate. |
| Administration | Profile and repository-mapping operations exist, but their configuration UX is unspecified. | Use authenticated configuration APIs for the pilot; add minimal Forge administration if needed. |

The supplied MCDA example is correct: scores `[4,3,4,3,5,3,5,2]` with the documented weights produce weighted score **3.80** and normalized index **70**.

### Forge visibility constraint

The Forge issue-panel button is currently visible only to users who can edit the issue. Confirm whether read-only stakeholders must access assessments. Supporting them may require another supported Jira surface or a later scope adjustment.

Source: [Jira issue panel documentation](https://developer.atlassian.com/platform/forge/manifest-reference/modules/jira-issue-panel/).

## 4. Security and implementation risks

### Repository access and disclosure

A GitHub App installation authorizes application access, but does not establish every Jira user's entitlement to private repository evidence. Define who may view evaluations and source snippets. Publishing private repository findings into a broader Jira audience also needs an explicit disclosure policy.

### Safe Roslyn analysis

Start with source parsing and safely parsed project metadata. Do not build, restore, run generators or analyzers, or load fetched projects through MSBuild-based mechanisms. Report reduced semantic coverage rather than implying complete call-graph analysis.

### Evidence integrity and model output

Give the model known evidence IDs. Validate returned references against retrieved SHA-pinned files and line ranges. A valid source location confirms that the cited source exists; it does not establish that every model interpretation is correct.

Treat Jira content, source files, comments, and model output as untrusted data. Strict output validation and application authorization remain necessary even with prompt-injection defenses.

### Publication reliability

A database uniqueness constraint alone cannot prevent duplicate Jira comments when a write succeeds but its response is lost. Track publication attempts, include a stable evaluation marker, and reconcile uncertain outcomes before retrying.

### Forge identity and background credentials

Validate the Forge invocation token, intended app, installation, tenant binding, and actor context. UI-supplied issue IDs are inputs, not authorization.

Represent installation identity and reinstall lifecycle in addition to `JiraCloudId`. Define expiry and refresh behavior for Jira tokens used by background work. Do not assume that request credentials remain valid indefinitely.

Source: [Forge authentication and installation guidance](https://developer.atlassian.com/platform/forge/remote/essentials/).

### Operational and privacy decisions

Before the integrated pilot, select:

- Model/provider and permitted data region.
- Retention and deletion periods for issue snapshots, evidence snippets, model proposals, and audit records.
- Repository file/size limits, context/token budgets, timeouts, concurrency, and tenant quotas.
- Latency and cost targets, retry policy, cancellation semantics, and recovery behavior.
- Credential storage and rotation strategy.

Reproducibility applies strictly to deterministic scoring for identical accepted inputs and profile versions. Do not assume a new AI call will reproduce an earlier proposal; preserve the original proposal and generation metadata.

## 5. Framework reuse assessment

Use selective reuse, with no framework references in Milestone 1.

| Existing component | Assessment and recommendation |
| --- | --- |
| `Framework.Persistence` and PostgreSQL registration | Accessible and small, but currently offer little beyond standard EF Core setup. Reconsider when persistence begins. |
| Generic CRUD repositories/controllers | Poor fit for evaluation revisions, tenant authorization, approvals, and publishing. Use explicit application operations. |
| Framework API host/error handling | Useful examples; the host is not a reusable Forge authentication solution. |
| `AIFramework.Core.IChatModel` | Built around grounded document answers rather than typed criterion assessment. |
| AIFramework Azure/AWS adapters | Potential transport reuse later, but current contracts lack structured assessment capabilities. The Azure adapter appends human-review text, which interferes with strict JSON output. |
| Knowledge ingestion/search pipeline | Unnecessary for the bounded repository-evidence MVP. |

Introduce a TaskMetric-owned `IAssessmentProvider`. At M4, compare adapting an existing model transport against using the selected provider directly. Do not expand shared frameworks merely to accommodate the first TaskMetric use case.

### Runtime baseline

Both frameworks target .NET 9. The locally installed SDK at review time was `9.0.308`.

Prefer **.NET 10 LTS**. The approved amendment permits M0/M1 with the available SDK; only .NET 9.0.308 is installed, so this increment pins net9.0 temporarily (ADR 0003). .NET 9 support ends on **10 November 2026**; migrate SDK/target/CI before further integration or release. No sibling framework migration is included.

Source: [Microsoft .NET support announcement](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/).

## 6. Proposed solution structure

```text
TaskMetricAI.sln
src/
  TaskMetricAI.Evaluation/       Pure scoring contracts and engine
  TaskMetricAI.Domain/           Evaluations, evidence, revisions, approvals
  TaskMetricAI.Application/      Use cases, orchestration, provider ports
  TaskMetricAI.Infrastructure/   Providers, Roslyn, EF Core, durable jobs
  TaskMetricAI.Api/              Forge authentication and HTTP endpoints
  TaskMetricAI.Jira/             Forge UI Kit TypeScript project
tests/
  TaskMetricAI.Evaluation.Tests/
  TaskMetricAI.Application.Tests/
  TaskMetricAI.ArchitectureTests/
  TaskMetricAI.IntegrationTests/
  TaskMetricAI.ContractTests/
  TaskMetricAI.SecurityTests/
  Fixtures/
docs/
  adr/
```

Dependency rules:

- `Evaluation` depends only on the .NET base libraries.
- `Domain` may reference scoring value types from `Evaluation`.
- `Application` references `Domain` and `Evaluation`.
- `Infrastructure` implements application ports.
- `Api` composes application and infrastructure dependencies.
- Forge calls backend APIs and does not implement scoring or hold provider secrets.

Create projects only when their milestone needs them. M1 needs the scoring library and its tests. Keep Forge in this repository; a separate `TaskMetricAI.Frontend` application is unnecessary.

## 7. Milestone roadmap

| Milestone | Deliverable and exit gate |
| --- | --- |
| **M0 — Minimal foundations** | Preserve the documentation, record initial ADRs and threat boundaries, pin SDK and dependencies, establish build/test CI. No external service is required. |
| **M1 — Deterministic MCDA** | Pure Metric Catalog/standard template, arbitrary enabled/disabled metrics, tenant-bound immutable profile/evaluation snapshots, validation, scoring/bounds/bands/points/sensitivity and unit tests. |
| **M2 — Authorized provider reads** | Tenant management/isolation, catalog/profile persistence and delegated administration, lifecycle/version allocation and scoped assignment APIs; Forge identity, Jira/GitHub reads, repository mappings and pinned SHA. Prove cross-tenant denial and preserved history. |
| **M3 — Safe .NET analysis** | Bounded Roslyn inspection, candidate selection, verified evidence locations, and explicit limitations. Fixture tests prove no source execution. |
| **M4 — Integrated evaluation workflow** | Strict AI proposals and evidence validation, durable orchestration, cancellation/recovery, immutable history, overrides, and confidence policy. Prove invalid output and prompt injection cannot trigger tools or publication. |
| **M5 — Jira review and publishing** | UI Kit panel, status/evidence display, overrides, publication preview, and approval. Prove tenant isolation, stale-approval rejection, and safe duplicate/uncertain-write handling. |
| **M6 — Pilot and calibration** | Review 20–30 representative tickets, refine rubrics and team mappings, measure operational cost/latency, and document failure modes and retention procedures. |

Authentication must be proven in M2 rather than deferred until the UI milestone. Each milestone remains independently reviewable.

## 8. Detailed Milestone 1 scope

### Goal

Deliver a reusable, deterministic scoring library without HTTP, persistence, Jira, GitHub, Roslyn, AI, or framework dependencies.

### 8.1 Immutable scoring contracts

Implement:

- Profile identity/version and scoring-policy version.
- Criterion IDs, labels, decimal weights, rubric anchors, and stable ordering.
- Integer score `1–5` or explicit unknown.
- Configurable complexity bands and optional team point mappings.
- Immutable results with validation findings, weighted contributions, missing criteria, and provisional bounds.

Support arbitrary administrator-configured criteria and weights. The eight documented criteria/weights and suggested bands/mappings are illustrative fixtures, not hardcoded production policy. All five criterion-specific anchors are mandatory for every configured criterion. No profile is automatically created or activated in M1.

### 8.2 Strict validation

- Require nonempty, unique criterion IDs.
- Require all weights in `[0,1]` and at least one enabled metric, with enabled weights summing exactly to `1`.
- Require exactly one assessment for each enabled criterion; reject disabled/removed IDs.
- Reject duplicate assessments, unexpected IDs, and omitted entries; unknown must be explicit.
- Reject out-of-range or fractional scores and mismatched profile versions.
- Validate ordered, nonoverlapping classification and point-mapping intervals with full intended coverage.
- Never silently normalize an invalid profile.

### 8.3 Deterministic calculation

For complete positive-weight assessments:

```text
Weighted score:   S = Σ(wᵢ × sᵢ)
Normalized index: N = 100 × (S − 1) / 4
```

- Use decimal arithmetic.
- Return each criterion's contribution.
- Classify using the unrounded result.
- Round only for presentation.
- Return no point suggestion without a configured team mapping.
- Return the configured split recommendation at the highest default point band.
- Never derive hours from points.

The default provisional team mapping uses:

| Weighted score | Candidate points |
| --- | --- |
| `[1,1.5)` | 1 |
| `[1.5,2)` | 2 |
| `[2,2.5)` | 3 |
| `[2.5,3.25)` | 5 |
| `[3.25,4)` | 8 |
| `[4,5]` | 13, with recommendation to split |

### 8.4 Unknown handling

For positive-weight unknowns:

- Calculate minimum and maximum using scores `1` and `5` respectively.
- Preserve unknown values and list missing information.
- Do not reweight known criteria.
- Return “requires refinement” without an authoritative scalar score or point candidate.

Approved zero-weight rule (docs/12): zero-weight unknowns remain visible but do not block the mathematical result.

### 8.5 Sensitivity analysis

- Perturb each weight by ±10% relative.
- Redistribute the remaining weight proportionally.
- Specify fixed decimal precision and deterministic residual allocation for recurring fractions.
- Report changed complexity bands or point candidates.
- Handle zero weights and sole-positive-weight profiles explicitly; report invalid or nonapplicable perturbations rather than fabricating a valid scenario.
- Label incomplete assessments as provisional rather than stable.

The exact precision and residual-allocation policy must be recorded in the scoring ADR and locked by tests before implementation is accepted.

### 8.6 Dependencies and exclusions

The production library should have no NuGet dependencies. The test project needs xUnit, its runner, and the .NET test SDK. Property-style cases can use deterministic generated inputs without an additional property-testing package.

Confidence aggregation, override audit storage, profile activation APIs, persistence, and integrations belong to later milestones. M1 explains the mathematics through contributions; source-grounded explanations arrive with evidence integration.

## 9. Milestone 1 tests

| Test area | Required checks |
| --- | --- |
| Reference vectors | All 1 → score 1/index 0; all 3 → score 3/index 50; all 5 → score 5/index 100; documented vector → score 3.80/index 70. |
| Validation | Empty/duplicate IDs, negative weights, invalid totals, invalid scores, missing/extra assessments, malformed bands, and profile-version mismatch. |
| Boundaries | Every point threshold, immediately below/above thresholds, maximum 5, and values such as 1.495. |
| Unknowns | One/all unknown, correct bounds, no fabricated result, and the approved zero-weight policy. |
| Determinism | Repeated calls, reordered equivalent inputs, different cultures, concurrent calls, and input immutability. |
| Properties | Score/index bounds, monotonicity, contributions summing to score, and unknown intervals containing all completions. |
| Sensitivity | Both perturbation directions, normalized totals, reproducible rounding, degenerate profiles, and classification changes. |
| Independence | No infrastructure/framework references; tests require no network, credentials, or database. |

## 10. Milestone 1 acceptance criteria

- [ ] Minimal foundations and scoring decisions are documented.
- [ ] A clean Release build succeeds on the pinned .NET SDK.
- [ ] All specified unit and deterministic property-style tests pass.
- [ ] The documented example reproduces score 3.80 and normalized index 70.
- [ ] Classification uses unrounded values with no mapping gaps.
- [ ] Unknowns never silently become neutral scores or definitive estimates.
- [ ] Profile/input validation rejects invalid data without silently repairing it.
- [ ] Sensitivity precision, normalization, and degenerate cases are documented and tested.
- [ ] The engine and its results do not mutate caller inputs.
- [ ] The library has no AI, EF Core, HTTP, provider, or shared-framework dependencies.
- [ ] Public contracts and a short usage example are documented.
- [ ] The milestone review records changes, executed checks/results, assumptions, and remaining work.

## 11. Decisions and prerequisites

M1 scope is authorized. Docs/12 and ADRs 0001–0003 resolve policy ownership, configurable rubrics/bands, zero-weight unknown handling, sensitivity precision and hashing. No production profile is automatically approved or activated; administrator configuration is M2+. M0/M1 implementation and verification evidence is recorded in docs/13_M0_M1_REVIEW.md.

Before M2, provide or select a sandbox Jira site, Forge app identity, dedicated GitHub App/test installation, permitted repositories, PostgreSQL environment, and repository-evidence access policy.

Before M4, select the AI provider/model and data-handling policy. Before M5, approve publication audiences and content, authorization rules, and stale-result behavior.

The owner has authorized documentation reconciliation, M0 and M1. Stop after the Release build/tests and milestone report for owner review before M2. See docs/12, CODEX_PROMPT_ADMIN_MCDA.txt, ADRs 0001–0003 and 13_M0_M1_REVIEW.md for the current scope and results.
