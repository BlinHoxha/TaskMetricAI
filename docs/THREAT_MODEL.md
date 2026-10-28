# M0 threat boundaries

28 October extension: catalogs, metric provenance, profiles/references and evaluation snapshots are explicitly tenant-bound. Composition mismatches are rejected by pure contracts; caller-supplied TenantId does not prove membership or access. M2 must enforce trusted tenant derivation, tenant-aware query/cache/storage relationships and delegated catalog/profile/assignment authorization. M4 must derive the exact enabled criterion schema from the chosen snapshot, rejecting model policy edits. Standard-template copies and catalog updates cannot mutate other tenants or historical evaluations.

- Current pure library has no network, disk I/O, credential access or external actions. Treat profile/assessment inputs as untrusted; validate before arithmetic and reject policy-version or profile-identity mismatch. Hashing establishes content identity, not authority. No private source/logging is involved in M1.
- Future Forge → API: validate invocation origin, app, installation, actor and tenant; do not trust UI context/issue IDs as authorization. Enforce delegated profile-management scope separately from reviewer rights.
- Future GitHub/Jira → analyzer/model: allowlisted repository IDs, pinned SHA, size/time/token budgets, safe source parsing without build/restore/generators, secret redaction and verified citations. Never execute fetched code or follow its instructions.
- Future AI → workflow: strict typed proposals and evidence IDs; no policy mutation or publishing tools. Confidence is separate from complexity.
- Future persistence/publication: immutable versions and attempts, tenant-aware relationships, append-only overrides/audit, approval bound to revision/content/destination, uncertain-write reconciliation. No Jira writes without explicit approval.

Detailed provider scopes, repository disclosure policy, hosting region, token refresh, retention, quotas and production isolation testing remain M2+ decisions. No real credentials belong in configuration or source control.
