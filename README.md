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

Read [the profile addendum](docs/12_ADMIN_CONTROLLED_MCDA_PROFILES.md), [reconciled assessment](docs/11_ARCHITECTURAL_ASSESSMENT_AND_IMPLEMENTATION_PLAN.md), and [ADRs](docs/adr/0001-admin-controlled-profiles.md). Authorized administrators/evaluation managers own profile weights, rubrics, bands and mappings; AI proposes scores only. M0/M1 are authorized; stop for review before M2. No standalone frontend or external integrations are included in this increment.

## Build and use M1

Latest verification: [final M1 sign-off](docs/16_M1_FINAL_SIGN_OFF.md), including source/test references, 100 passing tests, and remaining .NET 10/legacy-reader prerequisites. M2 awaits explicit approval.

Approved [metric-agnostic SaaS amendment](docs/14_METRIC_CATALOG_AND_SAAS_AMENDMENT.md): reusable standard/custom Metric Catalog, optional eight-metric starter definitions, enabled/disabled criteria, mandatory tenant identity, new profile versions and complete immutable evaluation snapshots. Only enabled weights must total 1. The engine has no fixed metric IDs/count. See [ADR 0004](docs/adr/0004-metric-catalog-and-tenant-snapshots.md) and the [point-by-point alignment review](docs/15_METRIC_CATALOG_ALIGNMENT_REVIEW.md). Tenant authorization/storage isolation and administration remain M2+.

Current local baseline: .NET SDK 9.0.308 / net9.0, permitted by the amendment because .NET 10 is not installed. Migrate to .NET 10 LTS before further integration or release (ADR 0003).

```powershell
dotnet restore TaskMetricAI.sln
dotnet build TaskMetricAI.sln -c Release --no-restore
dotnet test TaskMetricAI.sln -c Release --no-build --no-restore
```

Production `TaskMetricAI.Evaluation` uses only the .NET base libraries. Tests use xUnit, its Visual Studio runner and Microsoft.NET.Test.Sdk; versions are centralized. No provider credentials, database or network are needed by the tests after package restore.

See [engine contracts and usage](docs/MCDA_ENGINE_USAGE.md), the [original M0/M1 review](docs/13_M0_M1_REVIEW.md), and the [current amendment review](docs/15_METRIC_CATALOG_ALIGNMENT_REVIEW.md). The GitHub Actions workflow is prepared; no hosted CI run has occurred. M2 backend work has not started. The manually created/deployed Forge Hello World starter remains unchanged by this amendment.
