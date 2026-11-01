# Architecture decision index

Current as of 1 November 2026. This index references authoritative decisions rather than duplicating them. ADR 0009 records the owner-approved bounded Jira read increment; current validation is in IMPLEMENTATION_STATUS.md. Historical reports do not authorize further work.

| Decision | Record |
| --- | --- |
| Human-controlled criteria/weights, lifecycle, precedence and publication approval | [ADR 0001](../adr/0001-admin-controlled-profiles.md) |
| Decimal scoring, unknowns, hashes and hypothetical sensitivity | [ADR 0002](../adr/0002-deterministic-scoring.md) |
| Pure M1 boundary and updated .NET 10 baseline | [ADR 0003](../adr/0003-m0-m1-foundations.md), docs/18 |
| Optional starter criteria, arbitrary catalogs/custom metrics, tenant snapshots | [ADR 0004](../adr/0004-metric-catalog-and-tenant-snapshots.md) |
| Modular monolith, verified identity and planned read-only provider boundaries | [ADR 0005](../adr/0005-m2-security-and-persistence.md) |
| Immutable configuration persistence, explicit operator migrations and audit | [ADR 0006](../adr/0006-tenant-configuration-persistence.md) |
| Runtime transactions, role separation, HTTP boundary and mandatory PostgreSQL CI | [ADR 0007](../adr/0007-runtime-tenant-security-boundary.md) |
| Project-context fallback, local registered entitlements, assigned Active versions and revocation | [ADR 0008](../adr/0008-profile-resolution-entitlements-and-revocation.md) |
| Request-scoped Jira user reads, exact signed API base, actor/project checks, restricted DTO and no-disclosure transport | [ADR 0009](../adr/0009-bounded-jira-cloud-reads.md) |

ADR 0007 supersedes earlier session-binding/absent-HTTP/skipped-coverage descriptions. ADR 0008 and its 31 October delivery correction record the explicit runtime preconditions and operator-approved populated-migration scope manifest. No new authorization/fallback policy was invented for the correction.

Persistent constraints: master stays unchanged; M2 belongs on feature/m2-foundations. Preserve the published checkpoint, reviewed Jira increment and backup tag. Current owner approval covers final review, commit, normal matching-branch push, CI monitoring and evidence synchronization. Stop before CI corrections if it fails. No merge, rebase, branch switch, deployment or next increment. Existing roles/databases/passwords must not be recreated or rotated. Credential/reference contents stay outside Git and logs.

Distinguish source present, local tests, hosted CI and live-provider verification. Current results and remaining limits are in [IMPLEMENTATION_STATUS](IMPLEMENTATION_STATUS.md) and [SESSION_HANDOFF](SESSION_HANDOFF.md). READY is a local feature-checkpoint assessment, not permission to publish or full SaaS sign-off. Stronger credential-compromise isolation, provider entitlements, AI/Roslyn and advanced UI remain separately scoped work.
