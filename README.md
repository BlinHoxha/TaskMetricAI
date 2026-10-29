# TaskMetric AI

**Jira Cloud + GitHub + .NET repository intelligence + explainable MCDA evaluation.**

## Goal
From a Jira issue panel, analyze issue requirements and a mapped GitHub repository, propose evidence-based complexity scores, missing edge cases and technical subtasks, and publish only after approval.

## Start here
1. Read `docs/01_PRD.md` and `docs/02_FUNCTIONAL_REQUIREMENTS.md`.
2. Read `docs/04_MCDA_SPECIFICATION.md` for mathematical rules.
3. Use `docs/09_CODEX_MASTER_PROMPT.md` in Codex.
4. Implement Milestone 1 before external integrations.

## Package contents
Original v1.1 package: ten specifications, AGENTS.md and README.md. The repository now also contains the approved administrator-profile amendment, reconciled assessment, ADRs, and the M0/M1 pure scoring library with xUnit tests.

## Scope boundary
This is TaskMetric AI, not the full DevScope AI platform. Documentation generation and transcript intelligence are out of scope for this MVP.

## v1.1 architecture update
**Agent-first, minimal Jira frontend:** one bounded .NET evaluation workflow and a Forge **UI Kit** issue panel. No standalone React SPA, frontend hosting, or separate login. Read `docs/10_JIRA_UI_AND_AGENT_WORKFLOW.md` before implementing the Jira app. This package supersedes v1.0.

## Approved administrator-controlled profiles and current scope

Read [the profile addendum](docs/12_ADMIN_CONTROLLED_MCDA_PROFILES.md), [reconciled assessment](docs/11_ARCHITECTURAL_ASSESSMENT_AND_IMPLEMENTATION_PLAN.md), and [ADRs](docs/adr/0001-admin-controlled-profiles.md). Authorized administrators/evaluation managers own profile weights, rubrics, bands and mappings; AI proposes scores only. M0/M1 are approved. M2 implementation is paused pending explicit owner approval. No standalone frontend or external integrations are included in this increment.

## Build and use M1

M1 is approved; see the [final M1 sign-off](docs/16_M1_FINAL_SIGN_OFF.md) for source/test references and historical compatibility limits. The .NET 10 upgrade is complete. See the [baseline separation report](docs/18_M1_NET10_BASELINE_SEPARATION.md) for current validation and branch status. M2 remains paused.

Approved [metric-agnostic SaaS amendment](docs/14_METRIC_CATALOG_AND_SAAS_AMENDMENT.md): reusable standard/custom Metric Catalog, optional eight-metric starter definitions, enabled/disabled criteria, mandatory tenant identity, new profile versions and complete immutable evaluation snapshots. Only enabled weights must total 1. The engine has no fixed metric IDs/count. See [ADR 0004](docs/adr/0004-metric-catalog-and-tenant-snapshots.md) and the [point-by-point alignment review](docs/15_METRIC_CATALOG_ALIGNMENT_REVIEW.md). Tenant authorization/storage isolation and administration remain M2+.

Current baseline: .NET SDK 10.0.403 / net10.0. Install the exact SDK pinned in global.json. This machine also has an isolated SDK at .artifacts/dotnet/dotnet.exe; substitute that executable for dotnet below if the SDK is not installed on PATH. The isolated SDK is ignored by Git.

```powershell
dotnet restore TaskMetricAI.sln
dotnet build TaskMetricAI.sln -c Release --no-restore
dotnet test TaskMetricAI.sln -c Release --no-build --no-restore
```

Production `TaskMetricAI.Evaluation` uses only the .NET base libraries. Tests use xUnit, its Visual Studio runner and Microsoft.NET.Test.Sdk; versions are centralized. No provider credentials, database or network are needed by the tests after package restore.

See [engine contracts and usage](docs/MCDA_ENGINE_USAGE.md), the [original M0/M1 review](docs/13_M0_M1_REVIEW.md), and the [current amendment review](docs/15_METRIC_CATALOG_ALIGNMENT_REVIEW.md). GitHub Actions builds and tests the solution. The original mixed M1/M2 commit failed hosted dependency restore; the separated baseline must pass CI after an explicitly approved push. Unfinished M2 work is preserved on feature/m2-foundations; it is not a validated implementation. The manually created/deployed Forge Hello World starter remains unchanged by this amendment.
