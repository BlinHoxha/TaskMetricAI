# M2 local feature checkpoint

Date: 31 October 2026 (Europe/Budapest).
Scope: owner-approved review and one local commit of all accumulated M2 identity/configuration/persistence/security work on feature/m2-foundations. This is not M2 completion, a merge or production readiness.

## Git and review boundary

Reviewed pre-checkpoint HEAD: 118f4c7ea05b129cd44115244bf838e18855cfae. Stable master: 946eb5d049c65d157aa2fb1fd92938c61fd75443, unchanged. The index was empty before checkpoint staging. The cumulative review covered 15 modified tracked files and 52 untracked files, plus this checkpoint record: 68 intended files. It did not treat the latest correction as the complete M2 diff.

This record is part of the approved local checkpoint; identify the resulting SHA with git log/show rather than mistaking the pre-checkpoint base above for the new tip. No push, merge, rebase, branch switch, master update or follow-on implementation is authorized. Stop after the local commit. Historical reports' uncommitted/no-commit statements refer to their original review gates.

## Verified scope and evidence

- Verified Forge identity/installation binding, tenant-scoped persisted grants, registered local scopes, authenticated configuration APIs, assigned Active-profile fallback resolution and audited exact-grant revocation.
- PostgreSQL schema 3, 14 FORCE-RLS tenant tables, composite tenant relationships, immutable configuration/audit history, separate setup/runtime roles, startup/schema/ACL/isolation validation and explicit READ COMMITTED transactions.
- Explicit populated-v2 migration with operator-approved scope manifest, preservation/rollback/replay coverage, connection cleanup/IDOR/privilege tests and observed final-administrator lock contention.
- Pure M1 Evaluation source/tests, SDK target/pin and Forge starter match master. No engine policy or mandatory metric set was introduced.
- Latest executed Release build: zero warnings/errors. Retained final reports confirm 100 M1 + 111 backend = **211 passed, zero failed/skipped**. Backend includes 79 PostgreSQL/HTTP cases. Dependency audit reports no known vulnerable packages. See [docs/24](24_M2_REVIEW_CORRECTIONS.md) for commands, report paths, migration usage and exact limitations. No source/dependency/test changes were made after that validation during checkpoint preparation; documentation was reconciled for the authorized commit.
- Reviewed migration/operator instructions and current status/handoff. Populated upgrades require explicit approved registrations; no inferred entitlement or database reset. Runtime prerequisites reject missing/incompatible grants/schema/isolation.
- No credentials, local database files, passfiles, connection-reference files, .env files, generated SDK/build/test output or unrelated workspace files are included. Ignore checks and targeted secret-pattern scans were clean. This is not an exhaustive historical Git secret audit.

## Remaining M2 work and risks

Hosted GitHub Actions, fresh disposable Linux bootstrap and live Atlassian FIT/JWKS/HTTPS verification remain pending; no hosted success is claimed. Local registrations/grants do not establish live Jira/GitHub entitlements. Provider reads, commit-pinned repository workflows, evaluation-result persistence/isolation, Roslyn, AI and advanced UI remain incomplete. Production TLS, secret lifecycle and deployment assessment remain separate work. Custom-setting RLS trusts backend tenant selection and does not solve arbitrary SQL/runtime-credential compromise. Already-authorized work may finish after revocation.

No material blocker was found to this local feature checkpoint. Further publication/CI or development needs separate owner approval.

## Complete committed-file inventory

The categories below are mutually exclusive; migrations are executable source, separated here for review. Generated artifacts and local runtime configuration have zero entries and are excluded.

### Build, CI and ignore configuration (4)

- `.github/workflows/build.yml`
- `.gitignore`
- `Directory.Packages.props`
- `TaskMetricAI.sln`

### Documentation (19)

- `AGENTS.md`
- `docs/06_DATABASE_SCHEMA.md`
- `docs/17_M2_PLAN_AND_PREREQUISITES.md`
- `docs/19_M2_IDENTITY_AND_PERSISTENCE_INCREMENT.md`
- `docs/20_M2_SECURITY_REVIEW.md`
- `docs/21_M2_SECURITY_HARDENING_REPORT.md`
- `docs/22_M2_API_BOUNDARY_REVIEW.md`
- `docs/23_M2_PROFILE_RESOLUTION_AND_REVOCATION.md`
- `docs/24_M2_REVIEW_CORRECTIONS.md`
- `docs/25_M2_FEATURE_CHECKPOINT.md`
- `docs/adr/0005-m2-security-and-persistence.md`
- `docs/adr/0006-tenant-configuration-persistence.md`
- `docs/adr/0007-runtime-tenant-security-boundary.md`
- `docs/adr/0008-profile-resolution-entitlements-and-revocation.md`
- `docs/context/ARCHITECTURE_DECISIONS.md`
- `docs/context/IMPLEMENTATION_STATUS.md`
- `docs/context/NEXT_SESSION_PROMPT.md`
- `docs/context/SESSION_HANDOFF.md`
- `README.md`

### Migration source (2)

- `src/TaskMetricAI.Infrastructure/Persistence/PostgreSqlHardening.cs`
- `src/TaskMetricAI.Infrastructure/Persistence/PostgreSqlSchema.cs`

### Source and operator tools (24)

- `src/TaskMetricAI.Api/ConfigurationJson.cs`
- `src/TaskMetricAI.Api/ForgeAuthenticationHandler.cs`
- `src/TaskMetricAI.Api/Program.cs`
- `src/TaskMetricAI.Api/RuntimeStartupGuard.cs`
- `src/TaskMetricAI.Api/TaskMetricAI.Api.csproj`
- `src/TaskMetricAI.Application/AccessControl.cs`
- `src/TaskMetricAI.Application/AuthenticateTenant.cs`
- `src/TaskMetricAI.Application/ConfigurationService.cs`
- `src/TaskMetricAI.Application/MetricDocuments.cs`
- `src/TaskMetricAI.Application/Ports.cs`
- `src/TaskMetricAI.Domain/Access.cs`
- `src/TaskMetricAI.Domain/ConfigurationEntities.cs`
- `src/TaskMetricAI.Infrastructure/AssemblyInfo.cs`
- `src/TaskMetricAI.Infrastructure/Persistence/ConfigurationDbContext.cs`
- `src/TaskMetricAI.Infrastructure/Persistence/InstallationDirectory.cs`
- `src/TaskMetricAI.Infrastructure/Persistence/LocalDatabaseOperator.cs`
- `src/TaskMetricAI.Infrastructure/Persistence/RuntimeDatabase.cs`
- `src/TaskMetricAI.Infrastructure/Persistence/RuntimeInstallationDirectory.cs`
- `src/TaskMetricAI.Infrastructure/Persistence/TenantCommandGuard.cs`
- `src/TaskMetricAI.Infrastructure/Persistence/TenantRepository.cs`
- `src/TaskMetricAI.Infrastructure/Persistence/TenantSessionInterceptor.cs`
- `tools/TaskMetricAI.Database/Program.cs`
- `tools/TaskMetricAI.Database/RuntimeRoleProvisioner.cs`
- `tools/TaskMetricAI.Database/TaskMetricAI.Database.csproj`

### Tests (19)

- `tests/TaskMetricAI.Backend.Tests/AuthenticationTests.cs`
- `tests/TaskMetricAI.Backend.Tests/AuthorizationTests.cs`
- `tests/TaskMetricAI.Backend.Tests/ConcurrencyHardeningTests.cs`
- `tests/TaskMetricAI.Backend.Tests/ConfigurationSecurityTests.cs`
- `tests/TaskMetricAI.Backend.Tests/ConfigurationWorkflowTests.cs`
- `tests/TaskMetricAI.Backend.Tests/HttpSecurityTests.Boundary.cs`
- `tests/TaskMetricAI.Backend.Tests/HttpSecurityTests.cs`
- `tests/TaskMetricAI.Backend.Tests/HttpSecurityTests.Resolution.cs`
- `tests/TaskMetricAI.Backend.Tests/PersistenceTests.cs`
- `tests/TaskMetricAI.Backend.Tests/PostgreSqlModelTests.cs`
- `tests/TaskMetricAI.Backend.Tests/PostgreSqlTests.cs`
- `tests/TaskMetricAI.Backend.Tests/ProfileResolutionTests.cs`
- `tests/TaskMetricAI.Backend.Tests/RelationshipHardeningTests.cs`
- `tests/TaskMetricAI.Backend.Tests/RuntimePrerequisiteTests.cs`
- `tests/TaskMetricAI.Backend.Tests/RuntimeSecurityTests.cs`
- `tests/TaskMetricAI.Backend.Tests/RuntimeSettingsTests.cs`
- `tests/TaskMetricAI.Backend.Tests/SchemaMigrationTests.cs`
- `tests/TaskMetricAI.Backend.Tests/SecurityFixture.cs`
- `tests/TaskMetricAI.Backend.Tests/TaskMetricAI.Backend.Tests.csproj`
