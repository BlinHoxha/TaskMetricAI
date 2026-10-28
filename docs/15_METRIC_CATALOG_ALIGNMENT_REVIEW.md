# Metric Catalog and SaaS amendment — alignment review

Date: 28 October 2026
Status: M0/M1 extension implemented and locally verified; stopped for owner review

Follow-up: the owner conditionally approved M1 and requested final source verification. See [16_M1_FINAL_SIGN_OFF.md](16_M1_FINAL_SIGN_OFF.md) for source/test traceability, SDK prerequisites, historical compatibility limits and the final 100-test run. The 95-case result below is the amendment baseline.

## Assessment

The previous evaluator was already metric-agnostic in its arithmetic and accepted arbitrary criterion counts/weights/bands/points. The gaps were reusable catalog definitions, explicit disabled membership, tenant identity in profiles/references and a full historical evaluation contract. Those gaps are now addressed at the pure M1 boundary.

Docs/14 is the approved amendment; ADR 0004 records the architecture. Updated the assessment, MCDA specification, administrator-profile addendum, roadmap, prior ADRs and related functional/security/AI/UI specifications before implementation. No microservices, new projects, new infrastructure, provider libraries or production NuGet dependencies were introduced. The user's manually deployed Forge Hello World starter was not edited or redeployed.

## Point-by-point check

| Owner requirement | M0/M1 alignment and evidence | Deferred implementation |
| --- | --- | --- |
| 1. Configurable Metric Catalog with reusable standard/custom definitions | MetricCatalog and immutable tenant-owned MetricDefinition versions. StandardMetricTemplate explicitly copies standard definitions into a tenant catalog. | Catalog persistence, management endpoints and discovery UI. |
| 2. Authorized admins add/remove/disable metrics, edit rubrics/weights/bands/points, manage multiple profiles and scoped assignments | Arbitrary metric snapshots, IsEnabled, configurable fields and independent profile identities/scopes; pure next-version copying. Tests add/remove/disable/re-enable and customize definitions without fixed IDs. | Administrator/delegated-manager authorization, mutation workflows, assignment resolution/APIs and UI. No claim these services already exist. |
| 3. Versioning and immutable snapshots; active edits create a new version | Profile.CreateNextVersion creates Draft N+1 while preserving the original. MetricDefinition.CreateNextVersion and EvaluationSnapshot retain exact inputs, original profile content/hash/version and result. | Transactional version allocation, activation and database uniqueness/concurrency. |
| 4. Strict metric/rubric/weight/band validation | At least one enabled metric; unique IDs across enabled/disabled entries; all five anchors; all weights [0,1]/precision; enabled total exactly 1; no repair; gap-free classifications/optional points. Existing validation suite plus new membership tests. | API error translation and admin editing UX. |
| 5. Tenant isolation | Mandatory tenant identity on catalog/definitions/profile/reference/snapshot; cross-tenant composition/reference rejection; tenant included in content hash. Same metric/profile IDs may exist independently in different companies. | Identity-derived tenant authorization, tenant management, storage/query/cache isolation and end-to-end security testing. Pure contracts alone do not enforce SaaS access control. |
| 6. Dynamic AI assessment of selected criteria | ActiveCriteria is a dynamic immutable list; scoring requires precisely its ID set. Documentation requires generating prompts/schemas from selected definitions/rubrics and profile identity. No fixed eight-field schema. | Actual AI request/response workflow and provider schema validation in M4. |
| 7. AI cannot mutate definitions or weights | Score proposals remain score-only contracts. No metric-management operation is connected to AI; policy mutation fields and tools are prohibited by the updated specification. | M4 adversarial JSON/prompt-injection tests and application/tool boundaries. |
| 8. Preserve historical evaluations | EvaluationSnapshot owns the exact immutable profile, accepted scores/questions and computed result. Tests prove later catalog/profile changes and removed metrics leave original content/results unchanged. | Durable storage, retention/export and append-only audit. |
| 9. Customizable standard default template | Optional eight-definition factory includes metric-specific rubrics, has no weights or activation, and is never referenced by scoring. Tests reproduce original 3.80 vector using explicit caller weights, and also score wholly custom profiles. | Authorized onboarding/configuration of actual company defaults. |
| 10. M1 domain contracts, engine, validation, snapshots and tests only | Changes confined to pure Evaluation assembly, .NET tests and documentation. No provider/framework/HTTP/EF dependencies. | All application integrations. |
| 11. Defer admin UI, persistence, assignments, authorization and tenant management | Roadmap and ADRs explicitly defer these. No such services/APIs/UI were implemented in this amendment. | M2+ subject to owner review. |

## Implementation details

- Catalogs contain one selected version per ordinal/case-sensitive metric ID and may be empty while being configured. Evaluation profiles must contain at least one enabled metric.
- Profiles embed complete metric definitions and optional catalog-origin references. Inline custom metrics remain supported without a catalog lookup. Catalog updates never change embedded profiles.
- Disabled metrics may retain valid prior weights but contribute nothing and are not assessed. Enabled zero-weight metrics remain visible and require a known/unknown assessment. Disabling/removing a positive-weight metric requires explicit rebalancing; otherwise the next profile is rejected.
- Sensitivity operates only on enabled metrics, retaining prior exact 12-digit allocation rules and explicit degenerate-case handling.
- New profile canonical schema is `profile-content-v2`; tenant, enabled state and catalog provenance are hashed. Constructor now requires TenantId and ProfileReference includes it: this is an explicit contract change. Historical v1 content/hash must be retained under its schema; no legacy persistence/deserialization migration is implemented or claimed.
- Pure next-version helpers increment and validate content; they do not provide authorization, persistent concurrency control or activation. The source profile/definition is unchanged.
- Standard-template Kind describes origin, not edit permission. Each tenant owns its copy; no global mutable company configuration exists.

## Executed validation

```powershell
dotnet build TaskMetricAI.sln -c Release --no-restore
dotnet test TaskMetricAI.sln -c Release --no-build --no-restore --logger 'trx;LogFileName=metric-catalog-tests.trx'
```

| Check | Actual result |
| --- | --- |
| Release build | Passed; 0 warnings, 0 errors |
| Full .NET test suite | 95 passed; 0 failed; 0 skipped |
| Prior regression coverage | All original 75 test cases pass with tenant-aware fixture/golden-contract updates |
| New amendment coverage | 20 additional cases: catalogs, custom sets/counts, enabled membership, tenant contracts, new versions and historical snapshots |
| Production dependency boundary | Only .NET base-library references; no package/project dependencies |
| Original documented scoring vector | 3.80 / normalized 70, including optional standard-template definitions |

TRX: `tests/TaskMetricAI.Evaluation.Tests/TestResults/metric-catalog-tests.trx` (generated artifact). No dependencies changed, so existing restored assets were used. No cloud calls, provider tests or remote CI run are included in this result. Forge starter testing was not rerun because this amendment did not change it.

## Remaining limitations and next review

Only .NET SDK 9.0.308 remains installed, so the previously documented temporary net9.0 baseline remains. Migrate to .NET 10 LTS before additional integration/release. This amendment did not install SDKs or alter sibling frameworks.

M0/M1 satisfy the approved contract/math scope. SaaS authorization/isolation, persistence, profile assignment/lifecycle APIs and dynamic AI execution remain specified future work, not implemented features. Review this amendment, contracts and passing results before authorizing M2. No M2 backend work was started.
