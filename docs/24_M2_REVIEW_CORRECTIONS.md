# M2 bounded review corrections

Date: 31 October 2026 (Europe/Budapest).
Branch: feature/m2-foundations. Status: locally validated; READY for an owner-approved feature checkpoint, not merge/production sign-off. No commit/push/merge has occurred.

## Findings resolved

1. Runtime preconditions now require default READ COMMITTED, exactly one schema marker with version 3, required SELECT/INSERT/DELETE/column-UPDATE/function grants, immutable schema metadata, read-only scope registrations/mappings and no direct registry access. Existing role/ownership/membership/DDL/TRUNCATE/FORCE-RLS checks remain. Startup and every runtime session call the validator. Each session additionally begins an explicit READ COMMITTED transaction. Missing/incompatible prerequisites fail closed (503); no migration occurs at startup.
2. Populated v2 migration uses an optional explicit operator-approved scope manifest. The transactional plan creates/prepares registrations, applies only supplied registrations for existing tenant identities, checks tenant mappings, validates composite FKs and advances version 3 only on success. Missing registrations yield migration_scope_manifest_required and roll back. No scopes or permissions are inferred from mappings. Retrying and reapplying are supported. No existing data is reset, deleted or recreated.
3. The final-admin test now holds one real revocation transaction open, observes the competing backend's ungranted advisory lock in pg_locks, verifies it is still blocked, commits the first revocation and verifies the second rejects removal of the final administrator. A bounded poll observes server state; there is no fixed-delay race assumption. Failure cleanup releases the first transaction and observes the competing task.
4. IMPLEMENTATION_STATUS.md and SESSION_HANDOFF.md now describe only current state, evidence and limitations. Historical milestones/counts live in reports 19–23 rather than mixed current tables/hash inventories. The continuation prompt and decision index reference current evidence explicitly.

## Changed code/test files in this correction

- src/TaskMetricAI.Infrastructure/Persistence/RuntimeDatabase.cs
- src/TaskMetricAI.Infrastructure/Persistence/PostgreSqlHardening.cs
- src/TaskMetricAI.Infrastructure/AssemblyInfo.cs (test-only internal access)
- tools/TaskMetricAI.Database/Program.cs (optional migration manifest argument)
- tests/TaskMetricAI.Backend.Tests/RuntimePrerequisiteTests.cs
- tests/TaskMetricAI.Backend.Tests/SchemaMigrationTests.cs
- tests/TaskMetricAI.Backend.Tests/HttpSecurityTests.Resolution.cs

Related documentation: this report, docs/06, docs/17, the historical marker in docs/23, ADR 0008 delivery amendment, AGENTS.md, README.md and the four docs/context documents. All preceding dirty M2 work is preserved. This list describes this correction, not the entire cumulative uncommitted feature diff.

## Migration/operator usage

Existing schema version remains 3; this correction does not introduce version 4. Both existing dev/test databases had their explicit migration/grant command reapplied. The new durable grant is SELECT-only access to TaskMetricSchema metadata for the matching runtime role. It is the explicit non-tenant metadata exception to the readable-table FORCE-RLS guard; runtime cannot change or truncate it. No roles, databases, passwords or passfile contents were created/recreated/rotated.

For populated v2 data, supply an operator-reviewed outside-Git JSON array of MigrationScopeRegistration records (tenantId, scopeType, scopeId). ScopeType 1 means Team; 2 means JiraProject. Example structure using fictional identities only:

```json
[
  { "tenantId": "example-company", "scopeType": 2, "scopeId": "example-project" },
  { "tenantId": "example-company", "scopeType": 1, "scopeId": "example-team" }
]
```

Run `dotnet run --project tools/TaskMetricAI.Database -c Release --no-build --no-restore -- grant-runtime <outside-Git-setup-connection-file> <approved-scope-manifest.json>`.
An already-registered v3 database or empty fresh setup needs no manifest. Do not use initialize alone for an unprovisioned populated-v2 upgrade: use the grant-runtime command with the approved manifest. Runtime access resumes only after migration and grants succeed. An aborted migration rolls back; a later grant failure leaves data migrated but runtime prerequisites fail until grants are corrected. Migration and grants retain their separate explicit transactions.

No automatic registration from historical mappings/grants is allowed. The manifest establishes scope existence only, not new actor grants. Operator authorization/review of the manifest is required outside the runtime API. There is no new public provisioning endpoint or general administration UI.

## Actual validation

SDK 10.0.403/net10.0, Release. Final build: **0 warnings, 0 errors**.

| Suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| M1 | 100 | 0 | 0 |
| Backend | 111 | 0 | 0 |
| Total | **211** | **0** | **0** |

Backend comprises 32 offline unit/SQLite/model cases and 79 PostgreSQL/HTTP cases (37 HTTP). Final TRX artifacts: both test projects' ignored TestResults/m2-review-corrections-final.trx. Backend finished 31 October 2026 at 13:13:43 +01:00.

Commands: `dotnet build TaskMetricAI.sln -c Release --no-restore`; `dotnet test TaskMetricAI.sln -c Release --no-build --no-restore --logger 'trx;LogFileName=m2-review-corrections-final.trx'`; `dotnet package list --project TaskMetricAI.sln --vulnerable --include-transitive --no-restore`.
Audit completed against configured NuGet sources: **no known vulnerable packages in eight projects**. Network escalation was needed after the sandbox blocked NuGet; no dependencies or audit enforcement were changed.

RuntimePrerequisiteTests exercises 13 negative configuration/ACL/schema mutations with rollback, explicit session isolation and the public runtime path with a serializable connection default. Owner-side transactional metadata checks use the same validator for the named runtime role; production always derives its role from current_user. Tests do not grant setup SET ROLE membership. The first targeted run exposed that prohibited impersonation in the test design (12 failures); it was corrected without weakening role separation. Corrected focused run: 15 passed, zero failed/skipped, before the final two prerequisite cases were added; final full run above includes them.

SchemaMigrationTests clones only uniquely generated synthetic fixture tenants into a rollback-only migration_test_* schema in the existing test database. Its v2-shaped dataset includes mappings, grants, immutable metric/catalog/profile documents, an active assignment, audit history, original FKs/RLS and immutable triggers; v3 registrations/FKs are omitted initially. It runs the actual migration SQL plan, proves failure rollback without a manifest, data equality before/after an approved migration, repeatability, schema 3, validated FKs and removal of the transactional test schema on rollback. Public schema/data and existing databases are not reset. Fresh empty Linux bootstrap still requires hosted verification.

## Remaining boundaries and review gate

Hosted GitHub Actions, fresh Linux provisioning and live Atlassian FIT/JWKS/HTTPS verification have not run for this uncommitted work. The existing mandatory PostgreSQL CI workflow will include the new cases after approved publication. No live Jira/GitHub entitlement, repository analysis, evaluation-result persistence, AI or advanced UI was added.

RLS trusts verified backend tenant selection; arbitrary SQL/runtime-credential compromise remains outside that guarantee. Revocations do not cancel previously authorized work. Scope-manifest approval and operator credential protection remain operational responsibilities. Existing runtime guards permit only the named local dev/test role/database pairs; production deployment/TLS/credential rotation remain separate work.

Master remains 946eb5d049c65d157aa2fb1fd92938c61fd75443, feature HEAD 118f4c7ea05b129cd44115244bf838e18855cfae. M1 engine/tests, Forge starter, SDK/target baseline remain unchanged. Stop for owner approval; READY is a local checkpoint assessment, not permission to commit/push/merge.
