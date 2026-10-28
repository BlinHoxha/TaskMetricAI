# TaskMetric AI — M0/M1 Implementation Review

Date: 27 October 2026  
Status: Implemented and locally verified; awaiting owner review before M2

Historical report: the 28 October metric-catalog/SaaS amendment extends these initial deliverables. See [current point-by-point alignment and results](15_METRIC_CATALOG_ALIGNMENT_REVIEW.md). The 75-test result below records the original baseline, not the amended suite. A Forge Hello World starter was subsequently created/deployed manually during setup; this amendment leaves it untouched.

## Outcome

Reconciled the original v1.1 package with the approved administrator-controlled profile amendment before implementation. Implemented M0 and M1 only. The production MCDA assembly has no NuGet packages, shared-framework references, HTTP, EF Core, Jira/GitHub clients, AI providers, database or external actions.

The attached original documentation and amendment were present only in ZIPs at the start. Their files have been preserved in this repository, including `docs/CODEX_PROMPT_ADMIN_MCDA.txt`. User authorization governs milestone scope; attached implementation instructions do not override it.

## Documentation and decisions

- Updated AGENTS.md, README, PRD, functional requirements, technical architecture, MCDA rules, persistence design, implementation plan, security, Master Prompt, Jira workflow, architectural assessment and amendment status.
- [ADR 0001](adr/0001-admin-controlled-profiles.md): authorized human policy ownership, immutable activated/used versions, archive-only retirement, project → team → organization precedence, ambiguity handling, and separation of validation from authorization.
- [ADR 0002](adr/0002-deterministic-scoring.md): exact weight totals/precision, unknowns, interval coverage, profile canonicalization/hash, and exact 12-digit sensitivity allocation with ordinal tie breaks.
- [ADR 0003](adr/0003-m0-m1-foundations.md): minimal projects/dependencies, temporary available-SDK baseline, framework reuse decision and stop boundary.
- Added [M0 threat boundaries](THREAT_MODEL.md) and [engine contracts/usage](MCDA_ENGINE_USAGE.md).

No product-policy ambiguity blocks M1: configured policy is supplied explicitly, and illustrative weights/rubrics/bands are not activated as production defaults. Actual production profile content still requires authorized human approval later.

## M0 deliverables

- Existing solution retained and populated with the pure Evaluation library and xUnit test project.
- SDK pin, central build settings, nullable analysis, warnings-as-errors, deterministic compilation and centralized test package versions.
- Build output, local IDE state and common secret-file exclusions.
- GitHub Actions restore/Release build/test workflow prepared with read-only repository permission.
- Threat model and ADRs recorded before application code.

The directory is not a Git repository and no remote CI run was performed. CI is prepared, not remotely verified. No repository was created or published. PostgreSQL/provider configuration was explicitly deferred to M2 in the reconciled roadmap.

## M1 deliverables

- Immutable, defensively copied profile snapshots with ID/version/name/description, scope, descriptive lifecycle/governance metadata, ordered configurable criteria, five anchors, bands, optional points and policy versions.
- Strict structured validation of exact weight sum 1, [0,1] range, configured percentage precision, anchors, identities, profile references, assessment completeness and interval coverage. Invalid weights are rejected without normalization.
- Culture-independent canonical JSON and SHA-256 profile content identity. Lifecycle/audit metadata is excluded so archival does not rewrite content identity.
- Decimal weighted scoring, normalized index and criterion contributions; classification uses unrounded values.
- Positive-weight unknowns preserve questions and return provisional score/index bounds without definitive score/class/points. Zero-weight unknowns remain visible and contribute zero without blocking scoring.
- Optional administrator-defined point mapping, including configurable split recommendation. No mapping means no points; no hours calculation exists.
- Hypothetical ±10% sensitivity with exact BigInteger largest-remainder allocation at 12 fractional weight digits. Scenarios never mutate the configured profile; unsupported perturbations are explicitly NotApplicable.
- All mathematical outputs carry the original exact profile reference. Constructing/validating a profile does not resolve scope, authenticate its creator or activate it.

## Executed checks

Final successful commands, executed in the TaskMetricAI repository:

```powershell
dotnet restore TaskMetricAI.sln
dotnet build TaskMetricAI.sln -c Release --no-restore
dotnet test TaskMetricAI.sln -c Release --no-build --no-restore --logger 'trx;LogFileName=m1-tests.trx'
```

| Check | Actual result |
| --- | --- |
| Package restore | Successful with approved network access |
| Final Release build | Successful; 0 warnings, 0 errors |
| Final xUnit/VSTest run | 75 passed, 0 failed, 0 skipped |
| Reference vector | Weighted score 3.80; normalized index 70 |
| Uniform reference vectors | All 1 → 1/0; all 3 → 3/50; all 5 → 5/100 |
| Production dependency boundary | Only .NET base-library assembly references; no package/project dependencies |

The sandbox initially blocked NuGet access and later writing a generated build-cache file. Approved restore/build execution resolved those environment restrictions. The final successful build preceded the final 75-test run; results above refer to the final compiled sources.

The generated TRX is under `tests/TaskMetricAI.Evaluation.Tests/TestResults/m1-tests.trx` (ignored build artifact).

### Coverage highlights

- Invalid totals 0.99/1.01/zero, out-of-range weights, duplicates, precision violations, missing anchors, unsupported policies and bad profile metadata.
- Custom human-defined weight sets, arbitrary criterion counts, independent bands and optional point mappings.
- Boundary ownership and gap/overlap rejection, including 1.495 and unrounded threshold behavior.
- Positive/zero-weight unknowns, exact bounds and every completion of a two-unknown example.
- Exact profile ID/version/hash mismatch rejection; canonical schema/field order, decimal-scale and culture independence, metadata exclusions and content-change detection.
- Input collection mutation resistance, read-only results, reordered equivalent assessments and 64 concurrent evaluations.
- Sensitivity residual tie breaks, input-order independence, sole-positive/zero/out-of-domain cases, unknown stability and exact seven-digit target perturbations.
- Deterministic generated checks across 300 scoring profiles and 100 sensitivity profiles: score/index bounds, monotonicity, contribution sums, valid scenario weights and exact scenario sum 1.

## Runtime and dependency limitation

Only .NET SDK **9.0.308** is installed locally, so this increment uses **net9.0**, as permitted by the approved amendment when .NET 10 is unavailable. No SDK was installed machine-wide. Migrate the SDK pin, target and CI to **.NET 10 LTS before further integration or release**; .NET 9 support ends 10 November 2026.

Production has no NuGet dependencies. Tests use xUnit 2.9.2, xunit.runner.visualstudio 2.8.2 and Microsoft.NET.Test.Sdk 17.12.0. No Framework or AI-Framework dependency was added; the prior source inspection found no benefit for pure M1 arithmetic.

## Deferred work and review gate

M2 has not started. Profile authorization, delegation, persistence, lifecycle transactions, effective-scope resolution and configuration APIs remain application work. Provider authentication, repository-evidence disclosure policy, sandbox credentials, provider scopes, hosting region and retention remain M2+ prerequisites. AI integration, Forge review, approval enforcement and Jira publication remain later milestones.

Before M2, review these deliverables and the runtime upgrade, then authorize the next milestone. No Jira, GitHub, AI, PostgreSQL, administration API or dashboard has been implemented.
