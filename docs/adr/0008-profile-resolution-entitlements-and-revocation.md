# ADR 0008: Project-context resolution, local entitlements and grant revocation

Date: 30 October 2026.
Status: Owner explicitly approved the business rules before implementation; implemented locally, awaiting review.

This extends ADRs 0001, 0005–0007. It does not change M1 arithmetic, profile schemas/hashes, provider authorization or publication policy.

## Entitlement authority

For this bounded increment, trusted operators provision tenant-local project/team registrations and project/team mappings. Persisted actor scope grants remain the permission authority; registration alone confers no permission. Existing organization-wide read grants and organization administrator semantics remain unchanged. All team/project reads and configuration commands, including administrator commands, first require registration in the bound tenant. Administrators cannot manufacture scope existence through the grant API. There is no public registration/bootstrap route, new provider integration or inference of live Jira membership.

ProjectRegistration and TeamRegistration use the existing composite tenant/entity keys, query filters and FORCE RLS. ProjectTeam has composite tenant-aware foreign keys to both registrations. Runtime can read registrations/mappings but cannot insert, update or delete them. Explicit trusted operator provisioning is a library operation, LocalDatabaseOperator.RegisterScopesAsync; it validates tenant/organization registration and atomically writes scopes, mappings and an audit record. It is not exposed by the HTTP host. No administrative registration UI/HTTP API is included.

## Resolution

Authorize the requested registered project before reading candidates. Determine candidate teams solely through server-owned tenant mappings. Select the explicit project assignment, otherwise a unique assigned team candidate, otherwise the organization assignment. No assignment allows fallback; missing configuration fails. A present but invalid, missing-target, inactive, mismatched, unsupported-schema or corrupt-hash assignment fails closed. Multiple team candidates are ambiguous and require authorized human action; no implicit tie-break, newest-version selection or AI choice exists.

Only the assigned Active version is returned, with project, selected scope and the immutable tenant/profile/version/hash-bound profile snapshot. A project-authorized caller may receive that exact team/organization fallback via resolution without gaining general direct read access to the broader scope. The endpoint accepts a project ID, not a selectable tenant/team/profile/version. Query parameters cannot override the selected profile.

One tenant-filtered SQL statement reads mappings, assignments and profiles, using no-tracking materialization. This gives a consistent candidate snapshot under PostgreSQL READ COMMITTED. An operation may return the valid snapshot it read just before a concurrent archive; it is not permission to publish or use that assignment indefinitely. Later evaluation/publication must revalidate their own lifecycle/approval conditions. Archiving removes assignments atomically under ADR 0007, so later resolutions can fall back when the override is absent. Resolution itself does not persist an evaluation attempt.

## Revocation

Only an authenticated tenant administrator may revoke an exact grant by tenant-bound grant ID, with a reason and atomic before/after audit. A foreign or missing grant is inaccessible. Removal of the final organization administrator is rejected.

Grant creation and revocation acquire the same transaction-scoped PostgreSQL advisory lock derived from the tenant before checking administrator authority or counting administrators. This serializes application grant mutations, including concurrent self-revocations. Hash collisions can cause extra serialization, not cross-tenant permission. Runtime grants include ScopeGrant DELETE; application authorization governs that capability. As with existing grant INSERT/RLS, arbitrary SQL or compromised runtime credentials are outside the trusted-backend security guarantee.

There is no cached authority attached to a JWT or repository instance. Every operation queries committed grants again. Once revocation commits, subsequent authorization checks deny the revoked permission, including with the same JWT or an EF context that previously loaded the grant. Work whose authorization completed before the revocation may finish; forced cancellation of already-authorized requests is not implemented or required by the approved rule. This guarantee assumes the runtime's existing READ COMMITTED transaction path; do not replace it with a long-lived repeatable-read authorization snapshot.

## Delivery and limits

31 October delivery correction: runtime now validates schema 3/grants/default READ COMMITTED and explicitly begins READ COMMITTED transactions. A populated v2 upgrade may supply an explicit operator-approved scope manifest to grant-runtime; preparation, registration, FK validation and marker advancement are one rollback-safe migration transaction. No scope is inferred from old mappings. Runtime receives SELECT-only schema-version metadata, excluded specifically from tenant-table FORCE-RLS checks; it cannot mutate that metadata. See docs/24 and its migration tests. The local entitlement/revocation business rules above are unchanged.

Explicit schema version 3 adds registrations, FORCE RLS and mapping foreign keys; runtime grants add read-only registrations/mappings and grant DELETE. No registration is inferred or backfilled from historical grants. Existing project/team configurations without explicit registration fail closed until an operator provisions them. Migrations/grants remain explicit operator actions, never startup/request behavior. No roles, databases or passwords are recreated.

See [the increment report](../23_M2_PROFILE_RESOLUTION_AND_REVOCATION.md) for actual test results and remaining limits. Live Jira/GitHub entitlements, mapping maintenance/revocation APIs, evaluation-result persistence, AI, Roslyn and advanced UI remain out of scope.
