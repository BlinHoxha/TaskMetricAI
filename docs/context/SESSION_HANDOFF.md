# TaskMetric AI current session handoff

Updated: 1 November 2026 (Europe/Budapest). Read AGENTS.md, IMPLEMENTATION_STATUS.md, the architecture decision index and ADRs 0001–0009 before subsequent work. Historical reports record their original review gates; current owner instructions govern authorization.

## Stop and preserve

The owner approved the completed Jira read-only increment for final read-only review, then commit and normal push to origin/feature/m2-foundations if no blockers, CI monitoring and documentation synchronization. Final review found no blockers in the intended 16 files and confirmed 76 new tests. Pre-publication base is 6d9a0be3f4448efccc1108226c64df6cb70026f2; identify the resulting implementation commit with git log until verified publication evidence is recorded. If CI fails, investigate and stop for owner approval before corrections. No merge, rebase, branch switch, deployment or next feature is authorized.

Repository: C:\Users\blinh\source\repos\TaskMetricAI; origin https://github.com/BlinHoxha/TaskMetricAI.git. Local and live GitHub branch tips were verified at 6d9a0be; local/remote master remain 946eb5d049c65d157aa2fb1fd92938c61fd75443. Preserve all modified/untracked feature work and the backup tag. Recheck Git state before doing anything.

Latest published checkpoint: cumulative M2 be1c783d, CI-only readiness correction 692c8654, documentation-only tip 6d9a0be. Fresh GitHub verification also found [run 41229732799](https://github.com/BlinHoxha/TaskMetricAI/actions/runs/41229732799) successful for that exact tip: 100 M1 + 111 backend tests, zero failed/skipped, zero build warnings/errors and no known vulnerable packages across eight projects. It does not validate the current uncommitted Jira increment.

## Last increment and architecture

GET /api/projects/{projectId}/jira/issues/{issueId} uses the existing verified Forge installation capability, registered-project read authorization and real runtime tenant transaction/RLS. The verifier carries signed app.apiBaseUrl in request context; the Jira route requires exact https://api.atlassian.com/ex/jira/{boundCloudId}. Existing configuration routes need no provider token/base. User-supplied tenant/actor/role/base/field selectors confer no authority.

Require one opaque x-forge-oauth-user credential, attached to individual HTTP requests only. /myself accountId must match the signed actor. Read issue project metadata before content and recheck ID/project afterward; Jira enforces the user's Browse Projects and issue security. Fail closed with safe errors. Existing organization read/admin scope semantics and already-authorized-work revocation semantics remain unchanged; administrator status never bypasses Jira permissions.

Return only id, key, projectId, summary, nullable descriptionText and componentIds. Description is bounded lossy ADF plain text; unsupported nodes/attributes, comments, custom fields, attachments, provider URLs and user objects are excluded. GET only, updateHistory=false, no redirect/cookies/retries/cache, bounded bodies/parsing, 15-second provider workflow and 20-second route deadlines. Jira HTTP factory logs and request/response diagnostic propagation are disabled; network/parser errors are replaced without private messages/inner exceptions. A loopback transport diagnostic test and Trace-level API log tests use synthetic sentinels. No token/content persistence, migration, new package or Forge starter change. Details: ADR 0009.

Important files: Application/JiraIssueService.cs and Ports.cs; Domain/Access.cs; Application/AuthenticateTenant.cs; Infrastructure/Authentication/ForgeIdentityVerifier.cs; Infrastructure/JiraCloudReader.cs; Api/Program.cs. New tests: JiraProviderTests.cs, JiraTelemetryTests.cs, HttpSecurityTests.Jira.cs; HttpSecurityTests.cs extends the existing factory.

Final local verification: Release build 0 warnings/errors; **100 M1 + 187 backend = 287 passed, 0 failed/skipped**, finished 1 November at 20:11:54 +01:00. Backend: 86 offline/loopback + 101 PostgreSQL/HTTP cases, including 59 HTTP. The increment adds 76 tests (53 provider, 1 transport diagnostic, 22 HTTP/PostgreSQL). Full baseline runtime/RLS/concurrency/migration suites ran. Direct/transitive audit: no known vulnerable packages across eight projects. Both final TRX reports are TestResults/m2-jira-read-final.trx under their test projects, ignored by Git. Details and evidence limits are in IMPLEMENTATION_STATUS.md.

Preserve the modular monolith, pure metric-agnostic deterministic M1 engine, exact weights/unknown semantics and immutable version/hash snapshots. AI remains unimplemented and cannot choose policy. No Jira publication without separate human approval. Existing configuration/profile resolution/revocation, schema 3, fourteen FORCE-RLS tables, composite relationships, READ COMMITTED sessions, runtime startup/ACL guards and final-admin serialization remain in place. RLS trusts backend context and does not protect arbitrary SQL under a compromised runtime credential.

## Database configuration and reproducibility

Existing PostgreSQL 17 on localhost:5432; taskmetricai_dev/taskmetricai_test; taskmetric_runtime_dev/taskmetric_runtime_test; operator taskmetric_setup. Never recreate roles/databases/passwords or read/display passfile contents. Connection references remain outside Git in C:\Users\blinh\AppData\Local\TaskMetricAI. The passfile remains C:\Users\blinh\AppData\Roaming\postgresql\pgpass.conf.

Use the ignored .artifacts/dotnet/dotnet.exe (SDK 10.0.403/net10.0). Full tests require TASKMETRICAI_TEST_CONNECTION_FILE pointing to taskmetricai_test.connection and TASKMETRICAI_RUNTIME_TEST_CONNECTION_FILE pointing to taskmetric_runtime_test.connection in that directory. Tests use only the isolated test database, with unique synthetic tenants; operator fixture seed operations are explicit and HTTP work uses runtime. Existing migration/security tests retain their rollback protections. No dev migration/grant command was run for Jira. If authentication fails, stop database work and report a safe error; do not change credentials.

Commands: dotnet build TaskMetricAI.sln -c Release --no-restore; dotnet test TaskMetricAI.sln -c Release --no-build --no-restore --logger 'trx;LogFileName=m2-jira-read-final.trx'; dotnet package list --project TaskMetricAI.sln --vulnerable --include-transitive --no-restore. NuGet audit initially hit sandbox network restrictions; its approved network-enabled retry succeeded without changing dependencies or audit policy. Reports are ignored TestResults artifacts. Do not equate a partial offline run with full security validation.

API runtime still needs TASKMETRICAI_RUNTIME_CONNECTION_FILE, TASKMETRICAI_FORGE_APP_ID and TASKMETRICAI_FORGE_ENVIRONMENTS. No setup credentials, anonymous bootstrap or startup migration is available to it. Populated-v2 upgrade scope-manifest rules remain in docs/24; this Jira increment needs no migration.

## Pending work and exact next action

1. Publish only the reviewed Jira increment with a normal push to the matching origin branch; monitor exact-SHA CI and synchronize verified results. If CI fails, investigate and stop before corrections. No further feature or master change is authorized.
2. Separately scope Forge remote wiring and an HTTPS endpoint. The unchanged Hello World starter has no remote/appUserToken forwarding. Confirm least-privilege Jira read scopes/consent, real app/environment/installation/cloud binding and registered canonical project/grants. Protect upstream hosting/proxy observability from sensitive headers/bodies. Do not deploy or reinstall without approval.
3. Then verify real FIT/JWKS, actor matching, issue-level denials, cross-project denials and no-disclosure behavior against a sandbox Atlassian installation. All current Jira provider responses are controlled fixtures; no live authorization success is claimed.
4. GitHub read/commit-pinned workflows, evaluation-result persistence/isolation, Roslyn, AI and advanced UI/publication remain incomplete and require separate authorization. Production identities/TLS/credential lifecycle, tenant quotas and deployment review remain outstanding.

Exact action for a new session: read both continuity documents and ADR 0009, verify branch/HEAD/working tree and the latest exact-SHA CI evidence. Complete only any pending authorized publication/evidence synchronization; stop before CI corrections or the next increment unless the owner separately approves.
