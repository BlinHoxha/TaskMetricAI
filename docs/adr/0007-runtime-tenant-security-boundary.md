# ADR 0007: Runtime tenant transactions and HTTP configuration boundary

Date: 30 October 2026
Status: Implemented locally; awaiting owner review

## Identity and runtime lifetime

AuthenticateTenant issues an AuthenticatedTenant capability only after signature/claim validation and exact enabled Forge installation binding. Its constructor is internal to Application. Runtime database sessions accept this capability, not a request-selected TenantIdentity. The API principal contains the verified actor; client tenant/actor/role claims or headers are not copied as authority. Configuration DTOs reject unknown members. A target actor/role in an administrator's delegation command is configuration data, not the caller's identity.

One runtime connection and explicit transaction are owned per API operation. set_config(..., true) binds the tenant to that transaction. The command interceptor checks the binding before every EF reader/scalar/nonquery operation, including already-open connections, and rejects missing transactions or mismatches rather than repairing them. Do not pool DbContext instances or share a connection between concurrent tenant operations. Npgsql connection pooling retains reset-on-close; multiplexing and No Reset On Close are rejected. Commit precedes writing HTTP success; disposal rolls back uncommitted work without depending on a cancelled request token.

The old session-scoped opening interceptor is restricted to the taskmetric_setup operator path used by setup/library fixtures. Runtime requests cannot select that mode or load setup credentials. Runtime startup/session guards reject setup identities, unsafe role attributes, memberships, owned schema objects, schema CREATE/TRUNCATE privileges, or readable tables without FORCE RLS.

RLS trusts backend tenant context. These controls protect normal application operations and accidental context misuse; they do not authenticate an arbitrary SQL client's custom setting or protect against a fully compromised runtime credential/application process. No arbitrary SQL endpoint is exposed.

## Database least privilege and migration

The existing taskmetric_runtime_dev and taskmetric_runtime_test logins are used only in their corresponding isolated databases. Neither login is recreated, given ownership, granted privileged memberships or given new passwords. They have no SUPERUSER/CREATEDB/CREATEROLE/BYPASSRLS privileges. Passwords/passfile remain outside Git. Reviewed per-object grants exclude DDL, TRUNCATE, trigger/policy management, immutable-content UPDATE/DELETE, bootstrap and direct installation-registry reads. Directory lookup uses one fixed-search-path SECURITY DEFINER SQL function, owned by setup, with no dynamic SQL and EXECUTE restricted to runtime roles.

An explicit advisory-locked version-2 migration adds structured audit fields, a tenant/organization registration key and installation FK, role/scope checks, catalog-entry identity validation and assignment/profile triggers. Old immutable content and audit rows remain unchanged; new audit metadata is nullable/defaulted for legacy rows. The API never initializes or migrates schema.

Assignment INSERT/UPDATE locks the referenced tenant/profile row and verifies active status and exact scope. Archiving holds the same profile row lock and removes assignments in a database trigger, including assignments committed while archive waited. This handles both race orderings under READ COMMITTED. Unique active-scope/version indexes and revision tokens remain in place.

## HTTP and verification boundary

The single ASP.NET Core host composes existing services with fallback authentication and resource authorization. Forbidden/missing scoped resources return the same not-found response; invalid authentication returns 401. No bootstrap, generic CRUD/SQL, provider or evaluation endpoint is exposed. Responses disable caching; JSON/body limits and safe error codes avoid publishing credentials or database details.

PostgreSQL/HTTP/runtime-role tests are mandatory in the full backend suite. Missing connection configuration fails rather than skips. CI provisions a disposable PostgreSQL 17 instance and random outside-Git credentials, builds with the pinned SDK and runs every suite plus audit. A hosted run still requires an approved commit/push. No new deployed service or standalone frontend is introduced.
