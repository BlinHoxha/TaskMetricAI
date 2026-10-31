# Current implementation status

Verified: 31 October 2026, Europe/Budapest. This document describes current source; historical counts, inventories and source hashes are not mixed into the current tables. [Report 24](../24_M2_REVIEW_CORRECTIONS.md) is the latest validation record.

## Current source and authorization boundary

Branch feature/m2-foundations; reviewed pre-checkpoint base 118f4c7ea05b129cd44115244bf838e18855cfae. Master remains 946eb5d049c65d157aa2fb1fd92938c61fd75443. The owner authorized one local cumulative M2 checkpoint commit; this document and [the full inventory](../25_M2_FEATURE_CHECKPOINT.md) are included in it. Identify its exact SHA with git log rather than treating the base SHA as the new tip. No push/merge/rebase/branch switch or additional implementation is authorized. Stop after the checkpoint; it is not M2 completion or production sign-off.

| Capability | Implemented and verified | Boundary |
| --- | --- | --- |
| M1 metric-agnostic deterministic MCDA | 100 tests passing; unchanged versus master | No required eight-metric set; pure versioned contracts |
| .NET 10 baseline | SDK 10.0.403/net10.0, centralized versions | No dependency changes in review corrections |
| Verified Forge installation binding | Real verifier and stored exact tenant binding exercised locally | Local signing keys; live Atlassian verification pending |
| Tenant/scope authorization | Registered project/team scope, explicit persisted grants, fail-closed HTTP | Local operator registrations are not live Jira membership |
| Immutable metric/catalog/profile configuration | Version/hash/provenance checks, lifecycle, revisions and append-only audits | No persisted evaluation results |
| PostgreSQL isolation | Schema 3, 14 FORCE-RLS tenant tables, composite keys/FKs, runtime/operator separation | Custom setting trusts backend; not arbitrary SQL credential compromise |
| Runtime preconditions | Default READ COMMITTED, exact schema 3 and required/security grants; explicit runtime transaction isolation | Startup/session reject incompatible prerequisites; SELECT-only schema metadata is non-tenant |
| Effective profile resolution | Project > unique mapped team > organization, exact assigned Active version, snapshot/hash validation | Fallback snapshot does not grant general broader-scope reads |
| Grant revocation | Administrator-only exact grant, reason/audit, fresh authority queries, serialized final-admin protection | Already-authorized work may finish |
| Configuration API | Authenticated versions/lifecycle/assignments/catalogs/metrics/grants/resolution/revocation | No public bootstrap/SQL/provider/evaluation endpoints |
| Explicit migrations | Populated-v2 manifest provisioning, FK validation, rollback and replay tests | No automatic entitlement inference, database reset or startup migration |
| Mandatory PostgreSQL CI | Workflow and CI-only disposable provisioning present | New hosted run/Linux bootstrap not performed |
| Operator scope management | Trusted registration library and upgrade manifest support | No general scope administration UI/API |
| Jira/GitHub integration | Ports/entities and security design only | Authorized reads and commit-pinned workflows pending |
| Roslyn, AI, advanced UI/publication | Not implemented in these increments | Further authorization required |

## Current validation evidence

Final Release build: 0 warnings/errors. Final full run: **211 passed, 0 failed, 0 skipped** (100 M1 + 111 backend). Backend: 32 offline unit/SQLite/model + 79 PostgreSQL/HTTP cases, including 37 HTTP. Backend run finished 31 October at 13:13:43 +01:00.

Evidence: tests/TaskMetricAI.Evaluation.Tests/TestResults/m2-review-corrections-final.trx and tests/TaskMetricAI.Backend.Tests/TestResults/m2-review-corrections-final.trx, ignored by Git. Direct/transitive audit completed with no known vulnerable packages across eight projects. Build/audit execution and commands are recorded in docs/24; hosted CI is not claimed.

| Security regression | Source references |
| --- | --- |
| Invalid Forge claims, installation binding, scope/role denial and IDOR | AuthenticationTests, AuthorizationTests, HttpSecurityTests |
| A/B identical IDs, fallback-only access, corrupt/missing/ambiguous profiles | HttpSecurityTests.Resolution, ProfileResolutionTests |
| Revoked grants, same-token/loaded-context authority, final administrator lock contention | HttpSecurityTests.Resolution |
| DDL/TRUNCATE/RLS/role escalation denial and connection cancellation/reuse | RuntimeSecurityTests, RuntimeSettingsTests |
| Invalid default isolation/schema/ACL/RLS, metadata access, public runtime rejection | RuntimePrerequisiteTests (13 negative mutations plus 2 runtime-session cases) |
| Populated v2 data/history, approved manifest, rollback/replay, validated FKs | SchemaMigrationTests |
| Assignment/archive locking, immutable history, cross-tenant relationships | ConcurrencyHardeningTests, RelationshipHardeningTests, PostgreSqlTests, PersistenceTests |

The first startup-negative fixture attempted prohibited SET ROLE and failed; it was corrected without changing membership. Final tests use transactional owner-side metadata checks for the runtime role and verify the public runtime path separately. All temporary schema/ACL mutations roll back; migration_test_* schemas do not persist. No security tests were skipped.

## Current files versus historical records

The owner-approved checkpoint includes the complete cumulative M2 diff, not only the latest corrections. Review corrections touch RuntimeDatabase.cs, PostgreSqlHardening.cs, Infrastructure/AssemblyInfo.cs, Database/Program.cs, RuntimePrerequisiteTests.cs, SchemaMigrationTests.cs and HttpSecurityTests.Resolution.cs; docs/24 lists that bounded correction. Docs/25 records the complete checkpoint inventory and excluded artifact/configuration categories. Recheck git status before subsequent work and preserve any later changes.

Historical evidence is intentionally retained in [docs/19](../19_M2_IDENTITY_AND_PERSISTENCE_INCREMENT.md), [docs/20](../20_M2_SECURITY_REVIEW.md), [docs/21](../21_M2_SECURITY_HARDENING_REPORT.md), [docs/22](../22_M2_API_BOUNDARY_REVIEW.md) and [docs/23](../23_M2_PROFILE_RESOLUTION_AND_REVOCATION.md). Their older counts, absent-feature descriptions, original schema/skip observations and hashes are historical, not current assertions. ADRs 0007/0008 and docs/24 supersede relevant older delivery details.

## Remaining limitations

Hosted CI/fresh Linux bootstrap, live Atlassian tokens/JWKS/HTTPS, provider entitlements, evaluation-result persistence/isolation and production TLS/credential deployment remain unverified or unimplemented. Scope-manifest review is an explicit operator responsibility. No claim of complete SaaS production readiness is made.
