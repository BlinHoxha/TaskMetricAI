# ADR 0001: Administrator-controlled evaluation profiles

Date: 27 October 2026
Status: Accepted architectural requirement (owner-approved docs/12)

## Decision

Amended 28 October 2026 by ADR 0004: tenant-owned Metric Catalog, arbitrary standard/custom metric sets and enabled membership are approved. Eight metrics are optional template data. Tenant-aware profile/evaluation snapshots are M1 contracts; tenant management, authorization, persistence and assignment services remain M2+. AI cannot modify metric definitions or membership during evaluation.

Only authorized System Administrators or explicitly delegated Evaluation Managers define criteria, weights, five score anchors, complexity bands and optional team point mappings. AI proposes scores/evidence and reads the resolved profile; it cannot choose, alter, normalize or activate policy. Reviewers change scores with reasons, not weights. Priority remains separate from complexity; points are never hours.

Profiles follow Draft → Validated → Active → Archived. Validation is not activation authorization. Activated or used content is immutable; changes clone a new draft version. Archive activated versions even if unused; never edit or delete their content. Historical evaluations retain exact profile ID/version/hash and weights. Status and audit metadata are outside the immutable content hash. Pure M1 snapshots are always immutable regardless of status; lifecycle services and editable drafts arrive in M2.

Effective precedence is explicit authorized Jira-project assignment, then team, then organization default. Components do not add precedence; equally ranked candidates require authorized human selection. Missing defaults fail safely; AI never chooses. Implement assignment resolution and tenant/delegation checks in M2, not inside the arithmetic library.

At most one active assignment per effective tenant/scope. Transactional activation, optimistic concurrency and append-only events record actor/scope/time/reason and before/after changes. New profile versions produce new evaluation attempts, preserving history. Publication approval remains separate, bound to exact evaluation revision, destination and content hash; score changes invalidate it.

## Consequences

M1 validates content and returns profile identity; it cannot establish actor authority or active assignment. No mandatory default profile, implicit redistribution, autonomous policy optimization, management API or admin dashboard is introduced. Example weights remain caller/test data; ADR 0004 adds an optional standard definition/rubric factory, never invoked by the evaluator. Explicit preview/confirmed redistribution may be added to administration later; sensitivity scenarios never change live policy.
