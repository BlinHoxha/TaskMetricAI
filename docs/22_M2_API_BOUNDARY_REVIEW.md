# M2 API boundary review and correction

Date: 30 October 2026. Branch: feature/m2-foundations.
Status: locally implemented/verified; stop for owner review. Not a complete M2 sign-off.

Subsequent owner-approved profile resolution/local entitlements/revocation and fresh results are recorded in [docs/23](23_M2_PROFILE_RESOLUTION_AND_REVOCATION.md) and ADR 0008. Pending-resolution statements below describe this earlier review.

## Scope and actual starting state

The owner resumed source review, fresh validation and the smallest architectural increment. This is not another handoff. The actual workspace already contained ConfigurationJson.cs, additional application null/collection validation and HttpSecurityTests.Boundary.cs beyond the preceding handoff inventory. Those changes were preserved rather than overwritten or attributed to this correction.

The first fresh Release build failed with three CS8604 errors: the compiler did not know that Require.That(false, ...) cannot return. The existing input validation was therefore not buildable under warnings-as-errors. Added DoesNotReturnIf(false) to the guard's condition parameter; this expresses its existing throwing behavior, without null-forgiving operators, warning suppression or weakened runtime validation.

The smallest selected increment completes and tests configuration input/authorization boundaries before adding effective-profile resolution. No provider integration, schema migration, runtime grants, role/password/database recreation or M1 modification was needed.

## Changes and source review

- src/TaskMetricAI.Domain/Access.cs: annotate the existing fail-fast guard so nullable flow analysis understands validated inputs. The guard still throws the same OperationException.
- src/TaskMetricAI.Api/ConfigurationJson.cs: reject duplicate JSON property names. A later role, scope ID or expected revision cannot silently replace an earlier command value. Existing required-constructor/null/unknown-member checks and optional point mapping handling remain in place. Stored-document readers and immutable content hashes are unchanged.
- tests/TaskMetricAI.Backend.Tests/HttpSecurityTests.Boundary.cs: three duplicate-field rejection cases and two actor cases exercising all six mutation route kinds. Every denial is checked for unchanged audit/content/lifecycle state. Previously present boundary cases also ran: missing fields, nested nulls, null collection entries, optional points, anonymous malformed input, concurrent A/B identical IDs, foreign-vs-missing denials and unsupported list/delete routes.

Reviewed actual API composition, Forge verifier/handler/binding, Application permissions/configuration operations, runtime transaction/role checks, EF command guards, explicit runtime grants/triggers, tenant relationships and security fixtures. The API takes authority only from verified installation binding and persisted grants, creates a fresh tenant session per operation, and commits before returning success. Forbidden scoped resources map to the same safe 404 as missing resources. Administrators' target actor/role fields are configuration targets, not caller identity. No public bootstrap route exists.

## Fresh validation

All commands used the existing isolated SDK 10.0.403, net10.0 Release configuration and existing outside-Git connection references. Tests ran only against taskmetricai_test with unique synthetic tenants; operator fixtures register/seed synthetic bindings, while HTTP/runtime operations use taskmetric_runtime_test. The existing legacy PostgreSQL fixture checks initialization; the already-installed schema is recognized, not recreated. No migration or grant command was invoked in this correction.

| Check | Actual result |
| --- | --- |
| Initial Release build | Failed: three CS8604 errors in partially present validation; corrected with the guard contract annotation |
| First full run after guard correction | 100 M1 + 76 backend passed; zero failed/skipped |
| Release build after duplicate-field/tests changes | Passed, zero warnings/errors |
| Focused HTTP authorization/input suite | 29 passed; zero failed/skipped |
| Final complete M1/M2 regression | **181 passed: 100 M1 + 81 backend; zero failed/skipped** |
| Final backend coverage | 26 offline unit/SQLite/model + 55 PostgreSQL/HTTP cases; 29 of the latter are HTTP cases |
| Both existing runtime logins | Fresh authentication succeeded to their matching dev/test databases |
| Test runtime metadata | No SUPERUSER/CREATEDB/CREATEROLE/BYPASSRLS, no schema CREATE, zero public relation ownership; 12 enabled FORCE-RLS tables |
| Direct/transitive vulnerability audit | No known vulnerable packages reported across eight projects; network retry outside sandbox required to reach NuGet |
| M1/Forge/SDK baseline | Source/tests, starter, global.json and Directory.Build.props match feature HEAD |

Generated evidence, ignored by Git:

- tests/TaskMetricAI.Evaluation.Tests/TestResults/m2-boundary-final.trx
- tests/TaskMetricAI.Backend.Tests/TestResults/m2-boundary-final.trx
- tests/TaskMetricAI.Backend.Tests/TestResults/m2-boundary-http.trx

Fresh full-suite command: `dotnet test TaskMetricAI.sln -c Release --no-build --no-restore --logger 'trx;LogFileName=m2-boundary-final.trx'`.
Fresh build: `dotnet build TaskMetricAI.sln -c Release --no-restore`.
Audit: `dotnet package list --project TaskMetricAI.sln --vulnerable --include-transitive --no-restore`.

The full suite executed cross-tenant filter bypass/raw SQL/FK rejection, HTTP IDOR and authorization, connection reuse/exception/cancellation cleanup, runtime DDL/TRUNCATE/policy/role-escalation denial, immutable history and both assignment/archive lock orderings. No security tests skipped. These are synthetic local results, not hosted CI/live-provider evidence. Runtime credential contents were not displayed.

## Remaining M2 gaps and priorities

1. **Effective-profile resolution is absent.** Current exact-scope assignment is implemented; project > team > organization precedence is only agreed architecture. ProjectTeam has a model/filter but no resolution service/HTTP operation, and current runtime grants do not expose that table. Do not claim a usable runtime resolver from model presence.
2. **Resolution authorization needs an explicit bounded policy.** Authorize the requested project before candidate lookup; derive team membership from tenant-owned server data, never caller-supplied team/tenant IDs. Decide whether project authorization grants access to the resolved broader-scope snapshot through this operation without conferring general team/organization reads. Deny missing, inactive, corrupt or ambiguous assignments; never let AI choose. Return exact tenant/profile/version/hash and selection provenance. Use a consistent read strategy and test concurrent archive/reassignment. Any required ProjectTeam runtime SELECT grant needs explicit operator application, not API DDL.
3. **Scope existence, membership and provider entitlements remain incomplete.** A stored team/project grant is not verified live Jira access. Administrator target scopes can be configured before provider-side existence is established. Keep provider routes unavailable until their authorization is implemented. Add grant-revocation API semantics and audit/concurrency tests separately.
4. **PostgreSQL defense has a trusted-backend limit.** Transaction-local custom-setting RLS and least privilege protect normal operations and context mistakes; a compromised runtime credential/arbitrary SQL client can choose its own setting. No user SQL interface exists. Runtime configuration currently permits only the named dev/test role/database pairs; deployment-specific identities/TLS/credential rotation remain future reviewed work.
5. **Verification gaps:** hosted GitHub Actions/fresh disposable Linux provisioning and live Atlassian FIT/JWKS/HTTPS smoke tests have not run for this uncommitted work. Local signing keys are substituted in HTTP tests; the real verifier, binding and database are exercised. Jira/GitHub reads, commit-pinned repository workflows, evaluation-result persistence/isolation, Roslyn, AI and advanced UI are not implemented here.
6. Structured audits contain relevant state summaries, not exhaustive object copies or an independently recorded effective caller grant set. Broader request fuzzing, operational rate limits and deployment security review remain outside the verified cases; no production-readiness claim is made.

Recommended next bounded increment after review: implement project-context effective-profile resolution with the authorization policy above explicitly confirmed, tenant-scoped server mappings, missing/ambiguity handling and A/B API/database tests. Required acceptance: deterministic precedence, no client-selected tenant/team authority, no broader-scope resource enumeration, immutable resolved identity, concurrent lifecycle safety and complete M1/M2 regression.

## Git and stop boundary

Current branch remains feature/m2-foundations at 118f4c7ea05b129cd44115244bf838e18855cfae; master remains 946eb5d049c65d157aa2fb1fd92938c61fd75443. Earlier local M2 work and this correction remain uncommitted. No branch switch, commit, push or merge occurred. No hosted workflow was triggered. Handoff files were not recreated or updated. Stop for owner review.
