# Current implementation status

Verified: 1 November 2026, Europe/Budapest. This is the authoritative current source/evidence record. Read with SESSION_HANDOFF.md and ADRs 0001–0009; older reports retain dated milestone results and review gates.

## Git and authorization

Branch feature/m2-foundations; pre-publication base 6d9a0be3f4448efccc1108226c64df6cb70026f2. Final read-only review found no blockers in the 16 intended application/test/documentation files and verified all 76 new cases in the final reports. The owner authorized committing this reviewed Jira increment, a normal push to the matching origin branch, CI monitoring and evidence synchronization. Publication/CI results will be recorded after verification; identify the implementation commit with git log. Stop for approval before any CI correction. No merge, rebase, branch switch, deployment or next increment is authorized. Local and live remote master were verified at 946eb5d049c65d157aa2fb1fd92938c61fd75443 and remain unchanged.

Published history: cumulative checkpoint be1c783d6415f49e66d435558e9dec6fc3c2cd4d ([inventory](../25_M2_FEATURE_CHECKPOINT.md)); CI-only readiness correction 692c865490374c828b94cb44dcb0f1d0ab1b5c73; documentation-only tip 6d9a0be. Fresh read-only GitHub verification found the feature branch at that same tip and [run 41229732799](https://github.com/BlinHoxha/TaskMetricAI/actions/runs/41229732799) successful for it. Logs confirm Release build with 0 warnings/errors, disposable PostgreSQL bootstrap/runtime provisioning, 100 M1 + 111 backend tests with zero failures/skips, runtime-suite gate, no known vulnerable packages across eight projects, and cleanup. The prior [run 41227998388](https://github.com/BlinHoxha/TaskMetricAI/actions/runs/41227998388) passed for 692c8654. These are baseline hosted results, not verification of the current local Jira changes.

The checkpoint's first run 41223780762 failed on a Unix-socket readiness probe. A PostgreSQL crash was not established; temporary initialization-server behavior remains an inference. The approved TCP-readiness correction and unchanged workflow remain in place. Non-fatal action Node.js deprecation warnings remain a separate maintenance item.

## Implemented capabilities

| Capability | Current evidence | Boundary |
| --- | --- | --- |
| M1 deterministic metric-agnostic MCDA | 100 tests passing; source/tests unchanged versus master | Pure immutable versioned contracts; no provider/framework dependency |
| .NET baseline | SDK 10.0.403 / net10.0 | No dependency/pin changes in Jira increment |
| Verified Forge installation binding | Real verifier + exact enabled app/environment/installation/cloud directory lookup | Local signing keys in HTTP tests; live Atlassian FIT/JWKS pending |
| Tenant/project authorization | Registered scopes, persisted actor grants, fresh authority checks, fail-closed HTTP | Registration alone grants no access; local grants do not prove Jira permissions |
| Immutable configuration | Catalog/metric/profile version/hash/provenance, lifecycle, assignments and append-only administrative audit | Evaluation-result persistence remains absent |
| PostgreSQL isolation | Schema 3, 14 FORCE-RLS tenant tables, composite relationships, least-privileged runtime/operator separation | Backend-selected custom-setting RLS does not protect arbitrary SQL/compromised runtime credentials |
| Runtime prerequisites | Schema/grant/role/default READ COMMITTED checks; explicit per-operation READ COMMITTED transaction | No automatic migration/bootstrap; metadata SELECT exception remains non-tenant |
| Profile resolution | Project > unique mapped team > organization, exact assigned Active version and validated immutable snapshot | Fallback-only access does not grant general broader-scope reads |
| Grant revocation | Administrator-only exact grant, reason/audit, fresh authority and final-admin serialization | Already-authorized work may finish |
| Configuration API | Versions/lifecycle/assignments/catalogs/metrics/grants/resolution/revocation | No public bootstrap, SQL or evaluation endpoint |
| Jira issue read API | Local implementation and controlled-provider/real-PostgreSQL HTTP security verification; ADR 0009 | No live Atlassian verification, Forge remote wiring, issue/token persistence or writes |
| GitHub workflows | Port/entities/security design only | Concrete reads, repository entitlements and commit pinning pending |
| Operator scope registration/migration | Explicit library provisioning; populated-v2 approved-manifest rollback/replay/FK tests | No inference, general registration API/UI or Jira-increment migration |
| Roslyn, AI, advanced UI/publication | Not implemented | Separate authorization required |

## Bounded Jira increment

GET `/api/projects/{projectId}/jira/issues/{issueId}` accepts canonical positive decimal Jira IDs (at most 20 digits), no query selectors. Tenant/actor come from the verified installation capability. Existing registered-project read authorization runs before provider requests; existing organization read/admin semantics remain. Missing/invalid FIT, unknown installation, absent/revoked/foreign grants, unknown project, missing/invalid user credential, untrusted base, actor mismatch and provider denials fail closed.

The signed app.apiBaseUrl is carried in request context and must equal `https://api.atlassian.com/ex/jira/{boundCloudId}` exactly; no caller URL is accepted. Require one opaque x-forge-oauth-user credential, applied per outbound request. `/myself` accountId must match the signed actor. Fetch issue ID/project metadata before content and recheck ID/project afterward. Jira's as-user issue GET enforces Browse Projects and issue-level security; even local administrators cannot bypass provider denial. No system-token fallback.

The fixed DTO exposes only id, key, projectId, summary, nullable descriptionText and componentIds. Bounded, lossy ADF plain-text extraction omits unsupported nodes/attributes, marks/links, mentions, media and cards. No arbitrary JSON, comments, custom fields, attachments, user details or URLs are returned/fetched. GET-only fixed field requests use updateHistory=false. Bodies: 64 KiB actor/metadata, 1 MiB issue; JSON depth 32 and duplicate-name rejection; summary 1,024 characters, description 32,768 characters, 100 components and 10,000 ADF nodes. Invalid/excessive responses fail safely instead of truncating into success.

The provider workflow has a 15-second linked deadline including streaming; the route has a 20-second linked deadline covering authentication/database work too. No retries, redirect following, cookies or content/token caches. Jira HTTP factory logging and sensitive request/response diagnostic propagation are disabled. Transport/parser errors have safe messages without private inner exceptions. DTO/credential ToString methods are redacted. Trace-level API log tests and a real loopback HTTP diagnostic/EventSource test check synthetic secret/content sentinels. Normal no-store responses and indistinguishable forbidden/missing 404s remain; 429 supplies only a bounded numeric Retry-After delta, timeout 504, invalid provider data 502.

No persistence writes, migrations, role/database/password changes, new dependencies, infrastructure, Forge starter changes or M1 changes. A successful real-runtime API request was checked against complete before/after row snapshots for both synthetic tenants across every mapped tenant table, with no changes.

Modified code: Domain/Access.cs; Application/AuthenticateTenant.cs, Ports.cs and new JiraIssueService.cs; Infrastructure/Authentication/ForgeIdentityVerifier.cs and new JiraCloudReader.cs; Api/Program.cs. Tests: existing HttpSecurityTests.cs factory plus new JiraProviderTests.cs, JiraTelemetryTests.cs and HttpSecurityTests.Jira.cs. Documentation: this status, SESSION_HANDOFF.md, ARCHITECTURE_DECISIONS.md, ADR 0009 and README.md. Persistence/migration source, packages, SDK pin, CI, M1 and Forge starter remain unchanged.

## Local validation

Final Release build passed with **0 warnings, 0 errors** using the pinned ignored SDK. Final combined run: **100 M1 + 187 backend = 287 passed, 0 failed, 0 skipped**. Backend comprises 86 offline unit/SQLite/model/loopback cases and 101 PostgreSQL/HTTP cases (59 HTTP). The increment adds 76 cases: 53 Jira provider cases, 1 real loopback diagnostic case and 22 HTTP/PostgreSQL cases. Backend finished 1 November 2026 at 20:11:54 +01:00. Both final TRX counters were checked; all required tests executed.

Reports are ignored artifacts: both test projects' TestResults/m2-jira-read-final.trx, plus backend jira-focused.trx and jira-telemetry.trx. Commands: `.artifacts/dotnet/dotnet.exe build TaskMetricAI.sln -c Release --no-restore`; `test TaskMetricAI.sln -c Release --no-build --no-restore --logger 'trx;LogFileName=m2-jira-read-final.trx'`; `package list --project TaskMetricAI.sln --vulnerable --include-transitive --no-restore` with the same executable. Test connection references were supplied outside Git for the existing isolated taskmetricai_test only. The audit initially failed because sandbox networking blocked NuGet; its approved network-enabled retry completed with **no known vulnerable packages across eight projects**. No audit policy or package version changed.

Two test-compilation issues in the new tests (an ambiguous logging extension type and EF's interpolated-raw-SQL diagnostic) were corrected before successful execution. The table-snapshot helper uses fixed model identifiers and a parameterized tenant; no warning suppression. No production invariant was weakened.

Security coverage includes the existing full M1/M2 runtime/RLS/filter-bypass/privilege/concurrency/migration suites; Jira actor/tenant/project/issue substitution, same-FIT revocation, pre-provider authorization denial, concurrent tenant credentials with identical IDs, signed-base restrictions, allowlist projection, provider status/redirect handling, duplicate/malformed/oversized/streamed/deep content, ADF exclusions, bounded rate hints, actual provider deadline, streamed cancellation and log/diagnostic/error redaction. This is regression evidence, not measured full code coverage.

## Remaining prerequisites and limits

No live Jira/Atlassian FIT/JWKS/HTTPS request was made. HTTP tests replace Jira transport and signing keys, while retaining real verifier/binding/database authorization. The unchanged Forge Hello World app has no remote/appUserToken configuration. Live verification requires separately approved remote/HTTPS wiring, read scopes/consent (current classic read:jira-user and read:jira-work cover the chosen operations), exact app/environment/installation/cloud binding and canonical project registration/grants. Do not send credentials in chat or logs, deploy, reinstall or infer installation authority.

Hosted CI for the Jira implementation is pending the authorized publication; baseline CI is recorded separately above. Production identities/TLS, credential lifecycle, upstream proxy/hosting observability, tenant quotas and deployment security review remain separate. The no-disclosure boundary covers configured application/transport behavior, not arbitrary process instrumentation or memory compromise. Text is untrusted; future consumers must preserve rendering/prompt boundaries. GitHub, evaluation persistence/isolation, Roslyn, AI and UI/publication remain separately scoped work. This is not full M2 completion or production sign-off.

Next authorized action: publish only the reviewed increment with a normal push, monitor its exact-SHA CI, then synchronize verified evidence. If CI fails, investigate and stop for approval before corrections. No next feature or master change.
