# M1 final source-level sign-off

Date: 28 October 2026  
Disposition: M1 approved by the owner. The original verification below is historical; the .NET 10 upgrade is complete and M2 is now paused. See [current baseline validation](18_M1_NET10_BASELINE_SEPARATION.md).

## Findings

No blocking production-code defect was found in the reviewed M1 behavior. No production code, SDK/target settings, provider integrations or Forge files were changed. Added five focused regression tests in `M1SignOffRegressionTests.cs`; the full suite now contains 100 passing cases.

| Requested verification | Source and observed behavior | Relevant tests |
| --- | --- | --- |
| Disabled versus enabled zero-weight | [ProfileContracts.cs](../src/TaskMetricAI.Evaluation/ProfileContracts.cs) derives ActiveCriteria; [ProfileValidator.cs](../src/TaskMetricAI.Evaluation/ProfileValidator.cs) checks all definitions and exact enabled-weight total. [McdaEvaluator.cs](../src/TaskMetricAI.Evaluation/McdaEvaluator.cs) rejects disabled scores, requires enabled zero-weight scores/unknowns, and retains their zero contributions/questions. | [MetricAgnosticSnapshotTests.cs](../tests/TaskMetricAI.Evaluation.Tests/MetricAgnosticSnapshotTests.cs): `Disabled_metrics_are_excluded_but_enabled_zero_weight_unknowns_remain_visible`, active-total and disabled-definition checks. New sign-off test `Disabled_content_changes_identity_but_not_scoring_or_sensitivity_numbers`. |
| Definition/profile versions and immutable evaluations | [MetricCatalog.cs](../src/TaskMetricAI.Evaluation/MetricCatalog.cs) and ProfileContracts use checked version increments and immutable copied content. [EvaluationSnapshot.cs](../src/TaskMetricAI.Evaluation/EvaluationSnapshot.cs) captures exact profile, accepted scores/questions and computed result. Later catalog updates are never dereferenced. | [MetricCatalogTests.cs](../tests/TaskMetricAI.Evaluation.Tests/MetricCatalogTests.cs): catalog revision/tenant-copy preservation. MetricAgnosticSnapshotTests: add/remove/disable/re-enable and immutable snapshot tests. [M1SignOffRegressionTests.cs](../tests/TaskMetricAI.Evaluation.Tests/M1SignOffRegressionTests.cs): weight-only revision, adopted catalog rubric revision and integer-overflow regressions. |
| Tenant identity and cross-tenant references | Constructors require nonblank tenant identity. Catalog entries/source provenance must match owning tenant; evaluator compares complete tenant/profile ID/version/hash reference; EvaluationSnapshot checks its tenant against the profile. | MetricAgnosticSnapshotTests: `Tenant_identity_is_required_and_part_of_hash_reference_and_snapshot`, catalog-provenance rejection. MetricCatalogTests: cross-tenant entries rejected. |
| Content hashing and historical compatibility | [ProfileCanonicalizer.cs](../src/TaskMetricAI.Evaluation/ProfileCanonicalizer.cs) uses fixed-order profile-content-v2 JSON, invariant normalized decimal strings and SHA-256. Tenant, enabled flags and provenance are included; lifecycle/audit metadata excluded. Original immutable snapshots remain reproducible. Old v1 references are not silently accepted/rehashed as v2. | [IdentityAndIndependenceTests.cs](../tests/TaskMetricAI.Evaluation.Tests/IdentityAndIndependenceTests.cs): fixed canonical JSON, culture/scale/lifecycle invariance, content-change detection, defensive copying/concurrency. Sign-off test `Pinned_v2_hash_is_stable_and_a_legacy_v1_hash_is_not_silently_accepted` pins independent v1/v2 fixture digests. Legacy-v1 deserialization is not implemented. |
| Deterministic scoring, unknowns, sensitivity | McdaEvaluator performs decimal weighted sum/index and unrounded classification. Positive-weight unknowns produce bounds without definitive score/class/points; zero-weight unknowns do not block calculation. [SensitivityAnalyzer.cs](../src/TaskMetricAI.Evaluation/SensitivityAnalyzer.cs) uses exact 12-digit integer allocation/BigInteger remainders and ordinal tie breaks, without changing profile weights. | [ScoringTests.cs](../tests/TaskMetricAI.Evaluation.Tests/ScoringTests.cs): reference vectors, 1.495/thresholds, configurable policy and unknowns. [SensitivityTests.cs](../tests/TaskMetricAI.Evaluation.Tests/SensitivityTests.cs): exact residuals, order independence, skip cases, instability/partial results. [PropertyTests.cs](../tests/TaskMetricAI.Evaluation.Tests/PropertyTests.cs): 300 generated scoring profiles, 100 sensitivity profiles and completion bounds. |
| Custom metrics, changing weights, removed/disabled metrics, history | Catalog-backed and inline custom definitions use the same engine. New versions validate explicit weights without normalization; previous results and complete content stay unchanged. | MetricCatalogTests: custom-only reusable definitions. MetricAgnosticSnapshotTests: arbitrary counts, membership transitions, required explicit rebalancing and history. Sign-off regressions combine actual snapshots with weight-only/rubric revisions. |
| No mandatory eight criteria or infrastructure dependencies | McdaEvaluator, validators and SensitivityAnalyzer contain no standard-template reference or fixed metric-count check. [StandardMetricTemplate.cs](../src/TaskMetricAI.Evaluation/StandardMetricTemplate.cs) is an opt-in data factory. Production project has no package/project dependencies. | `Engine_accepts_arbitrary_metric_counts_without_standard_ids` covers 1, 3, 8, 20 and 50 custom metrics; `Custom_only_profile_needs_no_standard_metric_and_can_reuse_one_definition`; `Evaluation_assembly_has_only_BCL_references_and_no_packages_or_project_dependencies`. |

## Actual build and test results

```powershell
dotnet --list-sdks
# 9.0.308 [C:\Program Files\dotnet\sdk]

dotnet build TaskMetricAI.sln -c Release --no-restore
# Build succeeded. 0 warnings, 0 errors.

dotnet test TaskMetricAI.sln -c Release --no-build --no-restore --logger 'trx;LogFileName=m1-final-signoff.trx'
# Passed: 100; Failed: 0; Skipped: 0; Total: 100.
```

The initial sandbox build could not overwrite a generated test assembly (CS2012/access denied). The approved outside-sandbox rerun of the same Release build succeeded; the final test run used that newly built assembly. No dependency versions changed or additional packages were required. TRX: `tests/TaskMetricAI.Evaluation.Tests/TestResults/m1-final-signoff.trx` (generated artifact).

The suite exercises every requested M1 acceptance behavior. This is behavioral/regression evidence, not a claim of measured 100% line/branch coverage. No cloud integration, hosted CI or Forge deployment was performed in this verification.

## Remaining prerequisites and limits

1. **.NET 10 SDK unavailable.** PATH resolves to `C:\Program Files\dotnet\dotnet.exe`; SDK enumeration and SDK directory contain only 9.0.308. Install a supported stable **.NET 10 LTS x64 SDK for Windows** (not just the runtime). Then update global.json to the installed exact SDK version and Directory.Build.props TargetFramework to net10.0; CI already reads global.json. Rerun restore/Release build/tests before further integration/release. No unsupported target/SDK changes were made now.
2. **Historical compatibility is explicit, not automatic migration.** New v2 canonical content/hash is pinned; immutable current snapshots preserve their old contents/results. There is no v1 JSON reader, persistence or migration. Before importing any externally retained v1 evaluations, preserve their original schema/hash/result and implement/test a version-aware reader; never synthesize tenant/enabled fields and overwrite their old identity. Safe rejection of a v1 reference is tested, not v1 rehydration.
3. **Tenant IDs/hashes do not establish authorization.** M2 must derive tenant identity from authenticated context and enforce query/cache/storage isolation and delegated permissions. Atomic version allocation, one active assignment per scope and prevention of same-version content replacement are application/persistence responsibilities; constructors/next-version helpers do not provide a global registry.
4. **Rubric meaning and provenance require human/application governance.** M1 validates five nonempty anchors; it cannot verify semantic quality or prove an inline override matches an authoritative catalog record. The embedded exact profile content governs historical scoring. Scope resolution, administration, durable audit/retention and AI schema/tool restrictions remain explicitly deferred.

## Acceptance conclusion and stop boundary

**M1 meets its approved metric-agnostic pure-contract/scoring acceptance criteria on the available SDK**, with the SDK upgrade and deferred compatibility/application controls listed above. No fixed set of eight metrics is required. Production behavior needed no correction during this verification. Owner approval remains conditional until reviewed; M2 has not begun and requires explicit approval.
