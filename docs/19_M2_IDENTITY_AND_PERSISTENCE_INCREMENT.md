# M2 identity and persistence increment

Date: 29 October 2026
Branch: feature/m2-foundations
Status: Historical identity/persistence increment, validated on 29 October. The subsequent security hardening and HTTP/runtime boundary are recorded in docs/21 and ADR 0007. Statements below about absent HTTP components or skipped tests describe the earlier increment, not current behavior.

## Initial review and corrections

The previous draft verified Forge signatures/claims but did not bind them to a stored tenant. Identity fields were not validated, unknown role enum values could satisfy read checks, and no database enforcement existed. The approved fixture correction and EF dependency-security patches are retained.

Added exact installation binding, immutable validated tenant identity, explicit fail-closed permission checks, EF Core/Npgsql configuration storage, immutable metric/catalog/profile versions, lifecycle and assignment concurrency, composite tenant keys/relationships, PostgreSQL RLS/history triggers, and atomic application audit operations. The pure MCDA assembly and Forge starter remain unchanged.

Application operations support administrator catalog/metric versions and delegation, scoped profile creation/read/validation/activation/archiving and exact-scope assignment. Inline custom definitions remain supported; no mandatory metric set or normalization is introduced. See ADR 0006 for boundaries and explicit retirement/activation semantics.

## Local PostgreSQL prerequisite

The existing postgresql-x64-17 service runs PostgreSQL 17.2 on localhost:5432. The owner supplied an existing passfile and confirmed access as taskmetric_setup. Authentication was verified without reading or displaying that file. Both databases already existed, were owned by taskmetric_setup and had zero public tables. The role has CREATEDB but neither superuser nor bypass-RLS privileges. Schema version 1 was initialized in both databases; no databases or roles were created by this increment. No production/customer credentials or data were used.

Required setup role: LOGIN, CREATEDB, NOSUPERUSER, NOBYPASSRLS. Do not use this schema-owner setup role as a deployed runtime account. Two connection-reference files were created outside Git under C:\Users\blinh\AppData\Local\TaskMetricAI. They reference the existing passfile and contain no password. No passfile or server authentication setting was modified.

A connection file may contain `Host=localhost;Database=taskmetricai_test;Username=<development-role>;Passfile=<outside-Git-passfile>`; use a separate file with Database=taskmetricai_dev for development. Connection files and passfiles must be readable only by the intended local user. Do not enable sensitive-data logging or print connection strings.

Once credentials are configured, use the pinned SDK (`.artifacts/dotnet/dotnet.exe` on this machine):

```powershell
dotnet run --project tools/TaskMetricAI.Database -c Release -- initialize C:\outside-repository\development.connection
dotnet run --project tools/TaskMetricAI.Database -c Release -- initialize C:\outside-repository\test.connection
$env:TASKMETRICAI_TEST_CONNECTION_FILE = 'C:\outside-repository\test.connection'
dotnet test TaskMetricAI.sln -c Release
```

The operator is restricted to the two local database names and PostgreSQL 17. It checks ownership, never drops databases and only initializes an empty database or recognizes its existing schema version. Do not invoke initialization against other application databases. Never run integration tests against taskmetricai_dev: the test fixture requires taskmetricai_test. Tests use unique synthetic tenants and retain their synthetic history; they do not delete customer data or reset a shared instance.

## Validation log

1. Identity/authorization increment: 100 M1 + 8 backend test cases passed.
2. Relational storage guards: 100 M1 + 12 backend cases passed.
3. Catalog/profile workflow increment: 100 M1 + 15 backend cases passed.
4. Provider-model and lifecycle/security cases: 100 M1 + 21 backend cases passed; four PostgreSQL cases were initially skipped while credentials were pending.
5. PostgreSQL execution: all initial 25 backend cases passed with no skips. Metadata confirmed FORCE RLS on all 12 tenant configuration tables.
6. Signed-token-to-database-binding/atomic bootstrap and concurrent version allocation coverage: all 27 backend cases passed, including six real PostgreSQL integration cases, with no skips.

All six PostgreSQL cases require TASKMETRICAI_TEST_CONNECTION_FILE and are explicitly skipped when it is absent. The verified local run supplied it and executed all six against taskmetricai_test. Cases cover RLS with EF filters bypassed, no-tenant/raw-write rejection, composite relationships, unique versions, immutable database history, optimistic concurrency, signed installation binding, bootstrap transaction rollback and concurrent metric creation with no orphan audit. Synthetic test tenants/history remain in the isolated test database; taskmetricai_dev contains schema only. The initial initializer attempted a maintenance-database connection not covered by the passfile; it was corrected to initialize existing isolated databases directly, without changing authentication or credentials.

## Final verification

- `dotnet restore TaskMetricAI.sln`: succeeded for all seven projects, including the explicit local database operator. No package versions changed.
- `dotnet build TaskMetricAI.sln -c Release --no-restore`: succeeded, 0 warnings and 0 errors.
- `dotnet test TaskMetricAI.sln -c Release --no-build --no-restore --logger "trx;LogFileName=m2-identity-persistence-final.trx"`: **127 passed, 0 failed, 0 skipped** (100 M1 and 27 backend cases; six backend cases use real PostgreSQL 17.2).
- `dotnet package list --project TaskMetricAI.sln --vulnerable --include-transitive --no-restore`: no known vulnerable packages reported for any project, using the current configured sources/advisory data. Auditing and warnings-as-errors remain enabled.
- TRX files are in each test project's ignored `TestResults/` directory. Live runs set TASKMETRICAI_TEST_CONNECTION_FILE to the outside-Git test connection reference.
- PostgreSQL schema initialization succeeded in both already-existing isolated databases. No databases, roles, passfiles or global authentication settings were created/changed. Test-only synthetic history remains in taskmetricai_test; taskmetricai_dev has schema only.
- The initializer avoids requiring a maintenance-database credential when an approved isolated database already exists. Schema installation and bootstrap are explicit operator operations, never request/startup behavior.
- `git diff --check` passed. Evaluation source/tests, Forge starter, SDK/target settings and centralized dependency versions exactly match HEAD. A targeted repository source scan found no common provider-token/private-key patterns; this is not an exhaustive secret audit.
- master remains `946eb5d049c65d157aa2fb1fd92938c61fd75443`; feature HEAD remains `118f4c7ea05b129cd44115244bf838e18855cfae`. All changes are uncommitted; no branches were moved or pushed and no new hosted CI run was triggered.

## Remaining review boundary

No API host/endpoints, live Jira/GitHub reads, effective profile precedence resolution, Roslyn, AI workflow or advanced UI is included in this increment. No new infrastructure or production defaults are introduced. HTTP composition, operational credential separation and live-provider authorization remain later M2 work. Master remains at 946eb5d; changes are left local for review with no commit/push in this increment.
