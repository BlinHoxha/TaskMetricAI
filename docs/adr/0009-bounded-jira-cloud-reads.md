# ADR 0009: Request-scoped Jira Cloud issue reads

Date: 1 November 2026 (Europe/Budapest).
Status: Bounded implementation/field allowlist locally validated and final review found no blockers. Owner authorized commit, normal feature-branch push, CI monitoring and evidence synchronization. CI corrections require separate approval if a failure occurs; no deployment or next increment authority.

## Authentication and authorization

Extend ADR 0005 without changing tenant selection, grants, RLS or M1. The existing verifier retains signed `app.apiBaseUrl` in the authenticated capability, never a database column. Existing configuration-only requests need no Jira base or token. The Jira route requires exactly `https://api.atlassian.com/ex/jira/{boundCloudId}` using ordinal equality; aliases, ports, suffixes, extra paths, userinfo, queries, fragments and other clouds/products fail closed. No site URL/header/query controls the destination.

GET `/api/projects/{projectId}/jira/issues/{issueId}` accepts canonical positive decimal IDs of at most 20 digits and no query parameters. An exact installation binding selects the tenant. Existing registered-project read authorization applies, including existing organization read/admin rules. Registration alone grants no access. Local authorization completes before any Jira request. Work already authorized may finish after revocation, as in ADR 0008; subsequent requests requery grants.

Require exactly one nonempty bounded opaque `x-forge-oauth-user` header. The FIT remains the Authorization bearer, not the Jira credential. No system-token fallback, refresh, token parsing, shared default authorization, credential persistence or cache is added. The credential has private state and a redacted ToString. `/myself` must return an accountId matching the signed actor. Jira's as-user issue GET enforces Browse Projects and issue-level security; application administrator authority never bypasses it.

Read issue ID/project metadata first. Only a matching project permits the content GET; recheck the issue ID/project after that GET, including moved-issue races. A mismatch or provider 401/403/404 becomes the existing indistinguishable safe HTTP 404. Missing/invalid FIT or missing user credential returns 401. Bad client IDs/query parameters return 422. No human-approved publication capability is implied.

## Data and transport boundary

The DTO contains only id, key, projectId, summary, nullable descriptionText and componentIds. Request only project, summary, description and components with `updateHistory=false`; never request expansions, custom fields or comments. Project only the exact approved fields even if Jira returns extras. Description extraction is lossy plain text from supported ADF text/container nodes and hard breaks; marks, link attributes, mentions, media, cards and unsupported nodes are omitted. No URLs are followed and no HTML is rendered. Text remains untrusted input for any future consumer; no AI or execution exists here.

Limits: 64 KiB actor/metadata responses, 1 MiB content response, JSON depth 32, duplicate properties rejected, 1,024 summary characters, 32,768 extracted description characters, 100 components and 10,000 visited ADF nodes. Invalid/oversized data fails with safe 502 rather than partial success. The provider workflow has a 15-second linked deadline including streamed reads; the HTTP route has a 20-second linked deadline covering authentication/database work too. Caller cancellation propagates without private inner exceptions. Provider 429 returns a safe code and only a numeric Retry-After delta in [0,300] seconds; other hints are omitted. There are no retries.

Use the named Jira HttpClient with all factory loggers removed and all headers redacted. Its SocketsHttpHandler disables redirects, cookies and ActivityHeadersPropagator: the latter excludes diagnostic payloads containing request/response objects. A transport wrapper replaces network exception messages before HttpClient diagnostics; parsing/stream failures are replaced without inner exceptions. No application logging/telemetry of credentials or content is introduced. A real loopback transport test checks diagnostics/EventSource payloads; Trace-level API tests check logs and safe error responses using synthetic sentinels. This does not protect against arbitrary process instrumentation or memory compromise; future observability must preserve this boundary.

The runtime session performs authorization reads only, commits before success and retains the existing no-store response. No SaveChanges, provider-content audit, evaluation persistence, schema change, new package or Forge starter change is introduced. Tests compare all mapped tenant-table rows before/after a successful request for both fixture tenants.

## Verification and remaining prerequisites

Current executed results and source inventory belong in IMPLEMENTATION_STATUS.md and SESSION_HANDOFF.md. Contract/HTTP tests use controlled Jira transport; HTTP tests retain real FIT verification with local signing keys and real PostgreSQL installation binding/grants/RLS. They do not prove live Atlassian scopes, actor permissions or TLS integration.

Live verification requires a separately approved Forge remote/HTTPS configuration, appUserToken forwarding, Jira read scopes/consent, an exact operator installation binding and registered canonical project/grants. The unchanged Hello World starter has no such remote wiring. Scope selection must cover Get current user and Get issue, with no write scopes; current recommended classic read scopes are read:jira-user and read:jira-work. No deployment, reinstall, secrets exchange or live request is authorized here.

References checked against current official documentation: [Forge Remote contract](https://developer.atlassian.com/platform/forge/remote/essentials/), [remote product API calls](https://developer.atlassian.com/platform/forge/remote/calling-product-apis/), [Get current user](https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-myself/#api-rest-api-3-myself-get), [Get issue](https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-issues/#api-rest-api-3-issue-issueidorkey-get), [.NET SocketsHttpHandler source](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/SocketsHttpHandler.cs).
