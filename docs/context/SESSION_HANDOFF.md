# TaskMetric AI current session handoff

Updated: 31 October 2026 (Europe/Budapest). Current source/evidence only; prior milestones remain in their dated reports. Read AGENTS.md, [current status](IMPLEMENTATION_STATUS.md), [docs/24](../24_M2_REVIEW_CORRECTIONS.md), docs/17 and ADRs 0001–0008 before future authorized work.

## Stop and preserve

The owner authorized final review and one local commit of the complete cumulative M2 checkpoint. This handoff is included in that checkpoint; see [docs/25](../25_M2_FEATURE_CHECKPOINT.md). Stop after the commit. No push, merge, rebase, branch switch, master change, further commit or additional implementation is authorized. READY is a local feature-checkpoint assessment, not deployment authority or full M2 completion.

Repository: C:\Users\blinh\source\repos\TaskMetricAI; origin https://github.com/BlinHoxha/TaskMetricAI.git. Branch feature/m2-foundations; reviewed pre-checkpoint base 118f4c7ea05b129cd44115244bf838e18855cfae. Use git log to identify the local checkpoint commit containing this handoff. Master remains 946eb5d049c65d157aa2fb1fd92938c61fd75443. The checkpoint includes earlier identity/persistence/security/resolution work and review corrections. Preserve any subsequent modified/untracked work. Recheck actual status; cached remote refs are not fresh GitHub verification. Nothing has been pushed or merged as part of the checkpoint.

## Architecture and current security

Backend-first ASP.NET Core modular monolith, pragmatic Clean Architecture, no microservices, vector database or standalone frontend. Pure M1 Evaluation has no framework/provider dependency. Optional standard criteria are template data, never mandatory engine metrics. Exact decimal enabled weights total 1, with no normalization; disabled criteria and enabled zero-weight unknowns retain their separate meanings. Immutable tenant/profile/version/hash snapshots preserve history. AI later proposes configured scores/evidence only; it cannot change policy. Jira publication always requires explicit human approval. Preserve the owner's unchanged Forge UI Kit Hello World starter.

Forge signature/claim verification and exact enabled installation binding issue the tenant capability; client identity/role fields cannot override it. Registered project/team scopes plus persisted actor grants authorize configuration reads/writes. Resolution selects project, unique mapped team, then organization assignment, using only the assigned Active version. Invalid or ambiguous configuration fails closed. Project access permits the exact fallback snapshot without general broader-scope reads. Revocation is tenant-admin-only and audited; shared tenant advisory locking protects the final administrator. New authorization checks re-read committed grants; already-authorized operations may finish.

Runtime owns one fresh connection/context/transaction per operation. Default and actual transactions must use READ COMMITTED; schema 3 and reviewed grants are checked at startup/session entry. Tenant binding is transaction-local, with per-command checks and reset-on-close. Fourteen tenant tables use FORCE RLS and composite relationships; immutable content/history remain protected. TaskMetricSchema is specifically non-tenant metadata with runtime SELECT-only access. Setup is never the normal application identity. Custom-setting RLS does not protect a compromised runtime credential/arbitrary SQL client; no user SQL route exists.

## Database configuration and actions

Existing PostgreSQL 17 at localhost:5432; existing taskmetricai_dev/taskmetricai_test databases; existing taskmetric_runtime_dev/taskmetric_runtime_test logins. Operator/schema owner is taskmetric_setup (CREATEDB, not runtime). Never recreate roles/databases/passwords. Credentials remain outside Git; never print passfile or connection-file contents.

Passfile path: C:\Users\blinh\AppData\Roaming\postgresql\pgpass.conf.
Connection-reference directory: C:\Users\blinh\AppData\Local\TaskMetricAI.
Files: taskmetricai_dev.connection, taskmetricai_test.connection (operator); taskmetric_runtime_dev.connection, taskmetric_runtime_test.connection (runtime).

The latest correction reapplied explicit migration/grants to both existing databases, retaining schema 3 and adding runtime SELECT-only schema metadata. No existing data was reset. PostgreSQL tests use only taskmetricai_test and unique synthetic tenants. Negative metadata/ACL tests and isolated migration_test_* schemas roll back. No test schema is retained.

For populated-v2 upgrades, grant-runtime accepts an explicit outside-Git scope manifest; see docs/24 for JSON and the command. The plan prepares registrations, applies only operator-approved scope entries, checks mappings, validates FKs and advances the marker atomically. Missing entries fail and roll back. No actor permissions or scope existence are inferred from old mappings. Grants remain a separate explicit transaction; runtime prerequisites must pass before access resumes.

If authentication fails, report a safe exact error and stop database-dependent work pending owner resolution. Do not change passwords or read the passfile to troubleshoot.

## Verified results and reproducibility

Final Release build: 0 warnings/errors, SDK 10.0.403/net10.0. Full suite: **100 M1 + 111 backend = 211 passed, 0 failed/skipped**, finished 31 October 2026 at 13:13:43 +01:00. Evidence is each test project's ignored TestResults/m2-review-corrections-final.trx. Dependency audit: no known vulnerable packages across eight projects. Details and intermediate test-design failure/correction are in docs/24.

After future validation is authorized, use .artifacts/dotnet/dotnet.exe if the pinned SDK is not on PATH. Set TASKMETRICAI_TEST_CONNECTION_FILE to the outside-Git operator test reference and TASKMETRICAI_RUNTIME_TEST_CONNECTION_FILE to the runtime test reference. Full PostgreSQL tests fail rather than skip if configuration is missing. Run Release build/full tests/audit as recorded in docs/24. M1 alone needs no database. Tests must never run against development/customer data.

API configuration uses TASKMETRICAI_RUNTIME_CONNECTION_FILE for dev runtime plus TASKMETRICAI_FORGE_APP_ID and TASKMETRICAI_FORGE_ENVIRONMENTS. No setup secrets, anonymous bootstrap or automatic migration is available to the API.

## Remaining work and evidence boundaries

No hosted GitHub Actions/fresh Linux bootstrap/live Atlassian FIT/JWKS/HTTPS verification has run for current uncommitted changes. HTTP tests substitute signing keys only; they use the real verifier, binding and PostgreSQL. Jira/GitHub entitlements, provider reads, commit-pinned repository workflows, evaluation-result persistence/isolation, Roslyn, AI and advanced UI remain incomplete. Production TLS/credential lifecycle/patch review remains separate.

Historical reports 19–24 record earlier counts/states and pre-checkpoint Git observations; those statements are dated evidence, not the post-checkpoint Git tip. Current implementation status and docs/25 identify the checkpoint boundary. The next action is owner review, then separately authorized feature publication/hosted CI or another bounded M2 task. Preserve the approved architecture and stop here.
