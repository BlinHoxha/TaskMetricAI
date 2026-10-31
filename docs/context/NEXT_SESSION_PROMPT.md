# Fresh-session continuation prompt

Copy the prompt below into a new conversation. It provides context, not authority to implement or publish.

---

Continue TaskMetric AI at C:\Users\blinh\source\repos\TaskMetricAI. Read AGENTS.md, docs/context/SESSION_HANDOFF.md and IMPLEMENTATION_STATUS.md, docs/17, docs/24_M2_REVIEW_CORRECTIONS.md and ADRs 0001–0008. Reports 19–23 describe historical increments; use current status/docs/24 for current claims.

Expected branch: feature/m2-foundations. The owner authorized one local checkpoint commit containing the cumulative M2 work and docs/25_M2_FEATURE_CHECKPOINT.md; identify its actual SHA with git log. Its reviewed base was 118f4c7ea05b129cd44115244bf838e18855cfae. Master must remain at 946eb5d049c65d157aa2fb1fd92938c61fd75443. Inspect actual Git state, preserve any later changes and do not switch branches, create further commits, push or merge without approval. This checkpoint is not M2 completion or production readiness.

M1/.NET 10 is approved and unchanged. Preserve pure metric-agnostic deterministic MCDA, immutable version/hash contracts, exact enabled-weight sum 1/no normalization, explicit unknown handling and the Forge UI Kit starter. No standalone frontend, additional infrastructure, AI/Roslyn/provider workflow or Jira write is authorized.

Implemented M2: verified Forge tenant binding, persisted scoped grants, registered local project/team entitlements, exact assigned Active-profile resolution (project > unique mapped team > organization), limited fallback-snapshot access, audited grant revocation/final-admin protection, immutable PostgreSQL configuration, FORCE RLS/composite FKs, transaction-local tenant context and HTTP configuration APIs. Latest review corrections enforce default/explicit READ COMMITTED, schema 3/grants, explicit manifest-driven populated migration and observed competing revocation locks.

Latest actual local validation on 31 October 2026: Release 0 warnings/errors; **100 M1 + 111 backend = 211 passed, 0 failed/skipped**; dependency audit no known findings in eight projects. Retained reports: each project's TestResults/m2-review-corrections-final.trx. Hosted CI, fresh Linux provisioning and live Atlassian FIT/JWKS/HTTPS verification have not run for current changes. Configuration tests do not establish evaluation-result isolation or full production readiness.

Existing PostgreSQL databases and roles must not be recreated or passwords changed. Setup is operator-only; runtime credentials stay outside Git. Use reference paths in the handoff, never print contents. Populated v2 migration requires an operator-approved scope manifest; no inferred entitlements or resets. Negative migration/ACL tests use rollback-only schemas/transactions in the existing isolated test database. If authentication fails, report a safe error and stop dependent database operations pending owner resolution.

Current commit-readiness assessment is READY for an owner-approved feature checkpoint, not merge/production sign-off. First confirm context and wait for my concrete instruction. Do not resume implementation or publication based only on this prompt.
