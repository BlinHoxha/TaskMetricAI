# M2 plan and prerequisites

Status: Owner authorized the identity/authorization/PostgreSQL configuration increment on 29 October 2026, on feature/m2-foundations only. See docs/19_M2_IDENTITY_AND_PERSISTENCE_INCREMENT.md for current implementation and verification gaps. The owner subsequently authorized the bounded security-hardening/HTTP increment on 30 October 2026; docs/21 and ADR 0007 record its current scope and results. Further increments require review.

## Scope

Latest bounded correction: [docs/24](24_M2_REVIEW_CORRECTIONS.md) resolves startup preconditions, populated migration and deterministic final-admin concurrency review findings. Current status and fresh evidence are in docs/context/IMPLEMENTATION_STATUS.md; reports 19–23 are historical increment records. Stop for review before publication or additional M2 work.

Latest local increment: [profile resolution, local entitlements and revocation](23_M2_PROFILE_RESOLUTION_AND_REVOCATION.md), under owner-approved ADR 0008. Full M2/provider work and hosted verification remain incomplete. Docs/22 records the preceding API-boundary increment.

1. Install/use an official checksum-verified .NET 10 LTS SDK locally if absent, pin SDK/target, and pass the complete M1 regression suite.
2. Add lightweight Domain/Application/Infrastructure/Api projects. Preserve the dependency-free metric-agnostic Evaluation project and existing Forge starter.
3. Validate Forge Invocation Tokens (signature, issuer, audience, lifetime, app, environment, installation, cloud and actor). Resolve tenant only through an operator-provisioned installation directory. Load roles/delegations from server-side tenant storage; no UI-provided tenant or role authority.
4. PostgreSQL immutable metric/profile versions and JSON snapshots, scoped assignments, delegations, repository connections/mappings/access grants and append-only audit. Tenant query filters, save guards, composite tenant keys/relationships, active-scope/version uniqueness and concurrency controls.
5. Administrator/evaluation-manager configuration APIs with strict input validation, explicit profile validation/activation/archiving, organization/team/project resolution and immutable history.
6. Jira as-user reads with actor verification and bounded ADF/custom-field/comment retrieval. GitHub App short-lived tokens reduced to selected repository/contents+metadata read permissions; canonical repository IDs, explicit access grants and immutable commit pinning. No Jira publication or source execution.
7. Security, relational persistence, HTTP/authentication and provider contract tests after increments; record actual local/live verification separately and stop for review.

## Prerequisite audit

- Machine SDK initially only 9.0.308. Authorized download is installing an isolated SDK under ignored `.artifacts/dotnet`; no system-wide SDK installation is assumed.
- PostgreSQL 17 service/binaries are installed. TaskMetric-specific database credentials/choice of isolated cluster versus existing instance need local configuration. Never reuse unrelated application data or read/export credentials.
- Forge developer app exists and starter panel has been installed manually. M2 backend needs its approved app/environment/installation/cloud IDs and an HTTPS address for a live remote smoke test. No advanced UI work is included. A Forge CLI login token is not a Jira backend credential.
- Test GitHub repository, GitHub App ID/installation and local private-key reference have not been provided. Contract tests can run without live credentials; successful live GitHub access must not be claimed until configured.
- Initial administrator actor and tenant/installation binding must be provisioned through an operator-only bootstrap command, never an anonymous registration HTTP endpoint. The company/organization scope and explicitly delegated team/project scopes must be supplied.
- Sandbox subscriptions/Forge usage and a deployed HTTPS endpoint remain operator choices. No cloud deployment or paid hosting is implicitly selected.

## Acceptance and limits

M2 implementation is testable locally; missing external credentials are reported as live-verification gaps rather than silently bypassed. Authentication has no development anonymous shortcut. No AI workflow, Roslyn, advanced UI, microservices, queues, vector storage or automatic Jira write is included. Commercial Forge cloud support only initially; unsupported isolated-cloud invocations fail closed.

## Preservation status (29 October 2026)

This branch restores the original M2 implementation files without developing them further. It is based on the validated master baseline 946eb5d. Domain/Application/Infrastructure contracts, Forge verification and backend tests remain incomplete and unvalidated. M2 dependencies have not been restored or tested as part of this separation. No API host, EF database context/migrations, concrete Jira/GitHub integrations, or TaskMetric database have been added by this preservation operation. Previously recorded prerequisites remain subject to re-verification after approval.

The .NET 10 prerequisite is complete: SDK 10.0.403 and net10.0. The isolated SDK exists under ignored .artifacts/dotnet. See docs/18_M1_NET10_BASELINE_SEPARATION.md for the M1-only Release build and 100 passing tests. These results do not validate this branch's M2 solution.
