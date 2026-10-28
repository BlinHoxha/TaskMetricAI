# M2 plan and prerequisites

Status: Owner authorized; implementation in increments after .NET 10/M1 regression gate

## Scope

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
