# M2 security-hardening increment

Date: 30 October 2026
Branch: feature/m2-foundations
Status: Implemented locally; final validation recorded below. No commit/push/merge authorized.

Subsequent fresh source review, API input-boundary correction and 181-test validation are recorded in [docs/22_M2_API_BOUNDARY_REVIEW.md](22_M2_API_BOUNDARY_REVIEW.md). The 156-test results below remain the historical security-hardening run.

## Inputs and scope

Read AGENTS.md, docs/17, docs/19, ADRs 0005/0006 and the latest review in the preceding conversation (now recorded in docs/20). This increment addresses that review without changing the pure MCDA engine, M1 behavior or Forge starter. It adds no Jira/GitHub provider reads, AI evaluation, Roslyn or advanced UI.

## Changes

- AuthenticatedTenant is issued only through verified Forge installation binding. Runtime sessions take that capability; request fields cannot instantiate it.
- Transaction-local tenant binding, per-command guards, reset-on-close and no multiplexing protect connection opening/reuse, external-open connections, mismatched identity, exceptions and cancellation. HTTP success is written only after commit.
- Separate runtime dev/test logins use explicit least-privilege grants. Startup and session checks reject privileged identities or unsafe database settings. Operator credentials are unavailable to the API.
- Version-2 schema migration adds assignment/profile row locking and archive cleanup, catalog metric identity checks, tenant/organization registration constraints, role/scope constraints and structured append-only administrative audit details.
- The ASP.NET Core API requires Forge authentication by default and enforces current persisted grants inside the tenant session. Client role claims/identity headers are ignored; unknown request members are rejected. Foreign resources remain inaccessible. No public bootstrap route exists.
- PostgreSQL security tests cannot silently skip. CI provisions disposable PostgreSQL 17 with randomly generated credentials outside the checkout, runs complete tests and rejects skips/missing runtime coverage. No local role/database recreation is used by CI-only provisioning code.

## Local database actions and credentials

The owner confirmed both runtime roles and both databases already existed and prohibited recreation/password changes. Both logins were verified successfully using the existing pgpass.conf without reading or displaying its contents. No roles, databases or passwords were created, recreated or rotated locally. taskmetric_setup performed only the approved schema migration/object grants; application and new HTTP/security tests use taskmetric_runtime_test. Both roles were confirmed to lack SUPERUSER, CREATEDB, CREATEROLE and BYPASSRLS, to own zero public objects, and to lack public-schema CREATE, audit TRUNCATE and direct registry SELECT.

Outside-Git connection-reference files:

- C:\Users\blinh\AppData\Local\TaskMetricAI\taskmetric_runtime_dev.connection
- C:\Users\blinh\AppData\Local\TaskMetricAI\taskmetric_runtime_test.connection

They contain passfile references, not passwords. Existing setup connection references are retained for operator fixtures/migrations. User-supplied passfiles and server authentication settings are unchanged. Only TaskMetric development/test schemas and their grants changed. Synthetic test data remains in taskmetricai_test.

## Running locally

Use SDK 10.0.403 (this machine's isolated executable is .artifacts/dotnet/dotnet.exe). Before the full suite:

```powershell
$env:TASKMETRICAI_TEST_CONNECTION_FILE = 'C:\Users\blinh\AppData\Local\TaskMetricAI\taskmetricai_test.connection'
$env:TASKMETRICAI_RUNTIME_TEST_CONNECTION_FILE = 'C:\Users\blinh\AppData\Local\TaskMetricAI\taskmetric_runtime_test.connection'
dotnet restore TaskMetricAI.sln
dotnet build TaskMetricAI.sln -c Release --no-restore
dotnet test TaskMetricAI.sln -c Release --no-build --no-restore
dotnet package list --project TaskMetricAI.sln --vulnerable --include-transitive --no-restore
```

The pure M1 project still needs no database. For an explicitly partial offline backend run, use --filter 'Category!=PostgreSQL'; do not present that as full security verification. A deliberate missing-runtime-configuration run failed before database access, confirming required tests fail closed instead of skip.

For the local API, set TASKMETRICAI_RUNTIME_CONNECTION_FILE to the dev runtime reference and configure TASKMETRICAI_FORGE_APP_ID and comma-separated TASKMETRICAI_FORGE_ENVIRONMENTS, then run src/TaskMetricAI.Api. The host refuses setup credentials or missing Forge configuration. Real installation bindings must be provisioned separately by an operator; there is no anonymous development authentication or public bootstrap endpoint.

## Exposed configuration operations

GET /api/profiles/{id}/versions/{version}; POST /api/profiles/{id}/versions; POST /api/profiles/{id}/versions/{version}/lifecycle; POST /api/profile-assignments; GET/POST catalog versions; POST metric versions; POST /api/scope-grants. DTOs carry expected version/revision and change reason, not caller identity. All routes are authenticated and use the existing application permission checks. Immutable history has no delete endpoint.

## Verification

Increment runs passed 100 M1 + 41 backend cases, then 100 + 51, then 100 + 56 as race/relationship/privilege/cancellation cases were added. Final results, audit and remaining limits are recorded after final verification below.

Tests cover:

- Verified and invalid JWTs, unknown installations/actors, client claim/header spoofing and developer/manager escalation attempts.
- Tenant A/B HTTP reads, foreign lifecycle/assignment/version/catalog attempts and persisted tenant checks.
- Actual runtime-role DDL/TRUNCATE/RLS-policy/role-escalation denial, protected-content mutation and direct registry read denial.
- Same physical connection reuse, transaction cleanup, absent/mismatched already-open context, cancelled SQL rollback and exception rollback.
- Both assignment/archive lock orderings, catalog metric identity, installation organization FK, profile lifecycle constraints and structured role/scope audit.

## Limits and review boundary

- Hosted GitHub Actions and disposable Linux CI provisioning have not executed for these uncommitted changes. The workflow is configured and local mandatory-gate behavior was verified; do not claim hosted CI passed.
- Live Atlassian tokens/JWKS/cloud authorization were not exercised. HTTP tests use locally signed tokens through the real verifier and real PostgreSQL binding, replacing signing keys only.
- RLS is defense in depth for trusted backend tenant selection. Arbitrary SQL/database-credential compromise can select a custom tenant setting and is not solved by this increment. No such SQL interface is exposed to users.
- Effective project/team/organization profile resolution, provider entitlements, evaluation-result persistence/isolation and grant revocation APIs remain later work. No claims of evaluation-result isolation are made.
- Deployment TLS, operator/runtime credential lifecycle and production PostgreSQL patch/configuration review remain required. No deployment occurred.
- Master must remain at 946eb5d; all source/schema work is local for review and remains uncommitted on feature/m2-foundations.

## Final local validation

- Restore succeeded for all eight solution projects. The HTTP test dependency Microsoft.AspNetCore.Mvc.Testing was aligned to 10.0.14; MCDA dependencies and all M1 source/test files are unchanged.
- Final Release build: **0 warnings, 0 errors**.
- Final full-suite result: **156 passed, 0 failed, 0 skipped**: 100 M1 cases and 56 backend cases. The backend suite comprises 26 unit/SQLite/model cases and 30 PostgreSQL/HTTP security cases. Operator fixtures use setup for registration/seed work; HTTP and new runtime-operation paths use the unprivileged test login.
- Dependency vulnerability audit, including transitives: no known vulnerable packages reported for any project with the current configured sources. No warnings/audits were suppressed.
- Reports: tests/TaskMetricAI.Evaluation.Tests/TestResults/m2-hardening-final.trx and tests/TaskMetricAI.Backend.Tests/TestResults/m2-hardening-final.trx (ignored generated artifacts).
- Intentional negative gate: with runtime test configuration removed, a required HTTP/PostgreSQL test failed with a clear missing-configuration error and exit 1; it did not skip or access the database. That separate negative-gate report is not a failure of the final configured full suite.
- Both existing dev/test runtime logins authenticated successfully. Metadata confirmed their prohibited role flags are false, zero public object ownership, no schema CREATE, no audit TRUNCATE and no direct installation-registry SELECT. No roles/databases/passwords were recreated or changed; only the approved schema migration and object grants were applied.
- Both assignment/archive lock orderings completed without a dangling assignment. Tests wait for an actual PostgreSQL row-lock wait rather than relying on a fixed race delay.
- Git whitespace checks passed. Targeted common-token/private-key scans found no matches. Connection references/passfiles/environment files are ignored and actual credentials remain outside the repository. This is not an exhaustive secret audit.
- CI's no-skip/runtime-suite marker was checked against local result metadata. Hosted CI and fresh disposable Linux bootstrap remain unverified because nothing was committed or pushed.
- master remains 946eb5d049c65d157aa2fb1fd92938c61fd75443. Feature HEAD remains 118f4c7ea05b129cd44115244bf838e18855cfae, with this increment and the preceding local M2 work uncommitted. No branch ref, commit, push or merge was performed.

Recommended next step: review the uncommitted diff and approve publication for hosted CI verification. Further M2 profile-resolution/provider work remains subject to separate approval. This report is not a full SaaS production-readiness or evaluation-result-isolation sign-off.
