# M2 security review input

Review date: 29 October 2026. Recorded on 30 October 2026 from the owner's preceding review conversation.

The reviewed identity/persistence increment had 127 passing local cases but was not production-ready. Findings approved for the next bounded increment:

1. P0: taskmetric_setup owns the tenant tables, has CREATEDB and can modify schema/policies or truncate history. It must never be the application runtime identity.
2. P0: tenant context was session-scoped and set only on EF connection opening. Already-open connections, pooling, transactions, failure and cancellation needed explicit guards/tests. A custom PostgreSQL setting is backend context, not independent proof of authentication.
3. P1: assignment checked active status before saving; archiving could race and leave an assignment referencing an archived profile. Both operations must synchronize on the same database invariant.
4. P1: tenant relationships protected identity composition but semantic checks and installation tenant/organization consistency needed stronger database enforcement.
5. P1: administrative audits omitted relevant role/scope/target and before/after details.
6. P1: PostgreSQL tests could silently skip without configuration and were not required in CI. HTTP authentication/IDOR and runtime-role security were not yet implemented or validated.

The owner approved correcting these findings, adding a bounded ASP.NET Core configuration API and mandatory security tests on feature/m2-foundations. Master, the deterministic M1 engine and Forge starter must remain unchanged. No provider integrations, AI workflow, database-password changes, local role/database recreation, commit, push or merge is authorized. See docs/21_M2_SECURITY_HARDENING_REPORT.md for the resulting implementation and actual verification.
