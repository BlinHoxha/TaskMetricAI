# ADR 0003: M0/M1 foundations and dependency boundary

Date: 27 October 2026
Status: Accepted implementation scope under owner authorization

Preserve original documentation and reconcile with approved docs/12 before code. Create only the pure Evaluation library and xUnit test project, centralized package versions, SDK pin, ignores and CI workflow. PostgreSQL, HTTP, profile administration/authorization/lifecycle services, provider connectors, Forge and AI orchestration remain M2+. Do not create unused host or frontend projects.

Prefer .NET 10 LTS. Only SDK 9.0.308 is available locally; docs/12 explicitly permits the available SDK, so pin 9.0.308 with net9.0 for this local M0/M1 increment. No machine-wide SDK installation is included. Before further integration or release, migrate global.json, target framework and CI to .NET 10 LTS; .NET 9 support ends 10 November 2026. This revises the assessment's earlier hard prerequisite without changing scoring policy.

Production uses the .NET base libraries only (including JSON, cryptography and numerics). Tests use cached xUnit 2.9.2, runner 2.8.2 and Microsoft.NET.Test.Sdk 17.12.0. No Framework/AI-Framework references: inspected helpers provide no benefit for pure arithmetic and existing AI contracts do not supply structured criterion assessment. Reconsider adapter reuse in M4.

The directory is not currently a Git repository. Prepare a GitHub Actions workflow, but do not claim a hosted run or create/publish a repository. Local Release build/test commands provide current evidence. Stop for owner review before M2.

## 28 October 2026 baseline update

The owner approved M1 and the .NET 10 upgrade. global.json now pins SDK 10.0.403 and Directory.Build.props targets net10.0. The original SDK 9 and pre-Git observations above are historical. The repository is associated with BlinHoxha/TaskMetricAI on GitHub. M2 is paused; its unfinished implementation is preserved on feature/m2-foundations and excluded from the master solution. See ../18_M1_NET10_BASELINE_SEPARATION.md for current verification.
