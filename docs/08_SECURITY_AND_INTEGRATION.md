# Jira Cloud / GitHub Integration and Security

## SaaS metric policy boundary

Tenant IDs in pure M1 contracts detect composition mismatches; they do not authorize access. M2 must derive tenant from trusted identity and enforce it across catalog/profile CRUD, assignments, evaluation queries/caches and evidence. Administrators/evaluation managers alone own definitions/rubrics/membership/weights, with delegated scope checks. AI receives only enabled criteria and has no metric-management tools; reject policy mutation fields in its schema. Standard-template customization must never affect other tenants/history. See docs/14.

## Jira Cloud
Use Atlassian Forge issue panel for UI and authorized Jira API access. Verify latest Forge manifest permissions, app distribution constraints, external backend remote authentication and data residency implications in official Atlassian docs before implementation. Do not assume all Jira instances expose the same story-point field ID. Prefer a Jira comment or app-owned result initially.

## GitHub
Prefer GitHub App installation with repository-scoped read-only contents/metadata permissions, rather than broad personal access tokens. Resolve issue/project-to-repo mappings from admin-controlled configuration. Avoid exposing source text in Jira comments; use concise evidence links to authorized GitHub pages.

## Trust boundaries
Profile governance: enforce System Administrator/delegated Evaluation Manager scope server-side. Reviewer rights do not include profile editing. Validation does not authorize activation. AI has read-only profile context and no management or publishing tools. Audit lifecycle and overrides append-only; activation needs concurrency control and one active assignment per effective scope. These application controls are M2+, not guarantees supplied by the M1 library. See docs/12 and ADR 0001.

Jira text, repository files and AI output are untrusted inputs. Enforce tenant isolation at API and query layers, backend token validation, allowlisted repo IDs, no arbitrary URLs or file execution, model prompt isolation, output schema validation and explicit human approval.

## Secrets and data
Store credentials in managed secret store; encrypt at rest/in transit; redact source secrets and personal data; log IDs not content. Limit access to issue comments and code according to provider permissions. Set retention/deletion policy and audit access to evaluation reports.

## Operational safeguards
Rate limits, retry with jitter, circuit breakers, bounded concurrency, per-tenant quotas, cancellation, evaluation expiry, webhook signature verification where applicable, immutable commit pinning and idempotent publication.

## Deployment decision
Forge front end + independently deployed .NET API. Start with one sandbox Jira site and a dedicated test GitHub installation; production multi-tenancy only after authorization and isolation testing.

## v1.1 Forge UI Kit and agent safety
The Jira issue panel is an untrusted presentation boundary, not an authority to access arbitrary tickets or repositories. Verify tenant, user permissions and issue access server-side; use Forge-supported remote authentication patterns and least-privilege scopes. Do not expose GitHub tokens to UI Kit. The orchestrator cannot publish autonomously; publishing requires a separate user-approved action and idempotent backend validation. Review Atlassian's current data residency, remote egress and app distribution requirements before deployment.
