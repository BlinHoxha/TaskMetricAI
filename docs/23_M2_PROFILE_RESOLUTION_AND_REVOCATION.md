# M2 profile resolution, local entitlements and grant revocation

Date: 30 October 2026. Branch: feature/m2-foundations.
Status: implemented and locally validated; awaiting owner review. No commit/push/merge authorized.

Historical increment report: subsequent review corrections and current 211-test evidence are in [docs/24](24_M2_REVIEW_CORRECTIONS.md). The results and limitations below describe the original 30 October increment.

## Owner-approved rules and delivery

Before implementation the owner approved project-context access to the exact broader-scope fallback, local operator-provisioned registrations/explicit grants, assigned Active-version selection, fail-closed ambiguity/invalid configuration, administrator-only audited exact-grant revocation, final-administrator protection and the allowance for previously authorized work to finish. [ADR 0008](adr/0008-profile-resolution-entitlements-and-revocation.md) records the rules and boundaries.

Implemented:

- ProjectRegistration/TeamRegistration, tenant filters/composite keys, and tenant-aware ProjectTeam foreign keys. Scope reads/configuration now require registration even for administrators; registration alone grants no actor permission. Existing organization-wide read/admin semantics are retained.
- ConfigurationService.ResolveProfileAsync and a single no-tracking SQL candidate query: explicit project assignment, otherwise unique mapped-team assignment, otherwise organization default. Only the assigned Active version is returned. Missing configuration, ambiguity, invalid lifecycle/tenant/scope/schema/hash fail closed. Newer drafts never replace the assigned version. Teams come only from server mappings.
- Project authorization permits the exact resolved snapshot without granting arbitrary team/organization profile reads. Output includes project, selected scope, and immutable tenant/profile/version/hash-bound profile. Client tenant/team/profile/version fields cannot select the profile.
- Exact-grant revocation with reason and atomic before/after audit. Grant/revoke share a tenant-derived PostgreSQL transaction advisory lock before authority/count checks, preventing concurrent removal of the final administrator. Every later operation queries committed grants again, including the same token and a context that previously loaded the grant. Already-authorized work may finish.
- GET `/api/projects/{projectId}/effective-profile`; POST `/api/scope-grants/{id}/revoke` with `{ "reason": "..." }`. Existing authentication/error/transaction boundaries remain. No public registration/bootstrap or provider endpoint added.

Sources: Application/AccessControl.cs, ConfigurationService.cs, Ports.cs; Domain/ConfigurationEntities.cs; Infrastructure/Persistence/{ConfigurationDbContext,TenantRepository,LocalDatabaseOperator,PostgreSqlSchema,PostgreSqlHardening}.cs; Api/Program.cs. No M1 engine/versioned profile contract changes.

## Database/operator actions

Explicit schema **version 3** and reviewed grants were applied through existing operator tooling to the already-existing taskmetricai_test and taskmetricai_dev databases. New registrations use FORCE RLS; ProjectTeam references both registrations by tenant. Runtime receives SELECT on registrations/mappings and DELETE on ScopeGrant, with no registration/mapping write access. Initializer recognizes version 3; existing schemas are not recreated. CI's existing fresh setup calls the same migration/grant code.

No roles, databases, passwords or historical profile/audit content were recreated/changed. Synthetic scope/binding/test history was added only in taskmetricai_test; dev received schema/grants only. No registration is inferred/backfilled from historical grants: existing unregistered project/team configuration now fails closed until an operator provisions it.

Operator provisioning is `LocalDatabaseOperator.RegisterScopesAsync(setupConnection, identity, projects, teams, mappings, reason)`. It restricts local database/operator identity, verifies registered tenant/organization, and atomically saves registrations, explicit project/team pairs and audit. Missing/foreign registration references fail FKs. This trusted library operation is not exposed by HTTP; no scope-provisioning CLI/UI was added. Credentials and passfile contents stay outside Git and were never displayed.

## Fresh validation

Before changes: fresh Release build, 100 M1 + 81 backend tests, zero failures/skips. Existing dirty work and branch refs were verified rather than assuming earlier results.

Increment runs: 32 selected cases passed; then 100 + 93 full-suite cases passed. Final verification after adding same-project cross-tenant concurrency and missing-revocation-reason coverage:

| Check | Actual result |
| --- | --- |
| SDK 10.0.403/net10.0 Release build | 0 warnings, 0 errors |
| M1 | **100 passed**, 0 failed/skipped |
| Backend | **95 passed**, 0 failed/skipped |
| Backend breakdown | 32 offline unit/SQLite/model; 63 PostgreSQL/HTTP, including 37 HTTP |
| Complete final suite | **195 passed**, 0 failed/skipped |
| Direct/transitive vulnerability audit | No known vulnerabilities across eight projects; network retry required after sandbox blocked NuGet |
| PostgreSQL metadata | Schema 3, 14 FORCE-RLS tenant tables; runtime has no SUPERUSER/CREATEDB/CREATEROLE/BYPASSRLS, schema CREATE or public relation ownership; registration SELECT allowed/INSERT denied; ScopeGrant DELETE allowed |

Commands: `dotnet build TaskMetricAI.sln -c Release --no-restore`; `dotnet test TaskMetricAI.sln -c Release --no-build --no-restore --logger 'trx;LogFileName=m2-resolution-verified.trx'`; `dotnet package list --project TaskMetricAI.sln --vulnerable --include-transitive --no-restore`. Both test-project TRX reports are ignored artifacts in TestResults. No dependency versions or security enforcement changed.

Test references:

- HttpSecurityTests.Resolution.cs: precedence, assigned versus newer draft, archive fallback, fallback-only access, client selector/header spoofing, revoked project grant, unregistered/foreign scopes, ambiguity, stale-context/same-token revocation, foreign-grant denial, concurrent final-admin protection, runtime registration/mapping write denial, cross-tenant mapping FK, concurrent identical project/profile IDs across A/B.
- ProfileResolutionTests.cs: inactive/foreign/missing target, mismatched scope, corrupt hash and unsupported schema fail instead of fallback.
- HttpSecurityTests.Boundary.cs: revocation joins denied mutation routes; missing reason causes no mutation. Existing malformed-input/IDOR tests retained.
- Existing runtime/RLS/filter bypass/history/privilege/connection cleanup and assignment/archive race suites and all M1 tests executed. Older positive fixtures now explicitly register their known scopes; no negative security assertions were removed.

## Remaining limits and next steps

Local registrations/grants are not live Jira membership or GitHub repository entitlement. Operator mapping maintenance requires the trusted library; no removal/disable API or CLI/UI is implemented. Resolution reads one consistent candidate snapshot; an archive can commit immediately afterwards. Future evaluation/publication must revalidate its lifecycle/approval requirements. No evaluation attempt/result is persisted here.

Final-admin protection and revocation timing cover normal application operations, not arbitrary SQL under compromised runtime credentials. The custom-setting RLS trust boundary remains unchanged. Already-authorized work is not cancelled.

Hosted Actions, fresh Linux bootstrap and live Atlassian FIT/JWKS/HTTPS verification remain unexecuted for current uncommitted work. Local HTTP tests substitute signing keys while using the real verifier/binding/database. Provider reads, commit-pinned repository workflows, evaluation-result persistence/isolation, Roslyn, AI, advanced UI and production TLS/credential deployment remain incomplete. This is not full M2/production sign-off.

Next: owner review and explicit approval for hosted CI publication, or a separately scoped operator/provider increment. Stop here. feature HEAD remains 118f4c7ea05b129cd44115244bf838e18855cfae; master remains 946eb5d049c65d157aa2fb1fd92938c61fd75443. Nothing staged, switched, committed, pushed or merged; all prior M2 work is preserved. M1, Forge starter and SDK baseline remain unchanged.
