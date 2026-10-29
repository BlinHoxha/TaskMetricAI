# M1 / .NET 10 baseline separation

Date: 29 October 2026

M1 is approved. M2 implementation remains paused. The owner authorized local preservation, separation and baseline validation only; no push, deployment, database creation or further M2 development is included.

## Preserved history

The original published commit `1b609326a6f5f498e608bca958d3a94ebf31a05c` contains M1, the .NET 10 upgrade and unfinished M2 scaffolding. It remains reachable through `backup/m1-m2-mixed-2026-10-28`. No published history is rewritten.

`master` retains the pure Evaluation library, its tests, approved specifications and the unchanged Forge starter. M2-only Domain/Application/Infrastructure/backend-test projects and their package declarations are removed from this branch. M2 preparation documents are preserved with the unfinished work on `feature/m2-foundations`.

The preservation procedure creates a dedicated draft commit on feature/m2-foundations on top of the separated master baseline. Restoring that draft is preservation, not approval to resume implementation. Its M2 code and dependency versions remain unvalidated.

## SDK and validation

`global.json` pins SDK `10.0.403`; `Directory.Build.props` targets `net10.0`. Local validation uses the existing ignored `.artifacts/dotnet/dotnet.exe` SDK. No system-wide SDK installation is performed.

Actual local results:

- SDK: `10.0.403`.
- `dotnet restore TaskMetricAI.sln`: succeeded. The first sandbox attempt failed to retrieve NuGet vulnerability metadata (NU1900); the approved outside-sandbox rerun succeeded with auditing enabled.
- `dotnet build TaskMetricAI.sln -c Release --no-restore`: succeeded, 0 warnings and 0 errors.
- `dotnet test TaskMetricAI.sln -c Release --no-build --no-restore --logger "trx;LogFileName=m1-net10-separated-baseline.trx"`: 100 passed, 0 failed, 0 skipped.
- Report: `tests/TaskMetricAI.Evaluation.Tests/TestResults/m1-net10-separated-baseline.trx` (ignored generated artifact).
- Git comparison: Evaluation sources, Evaluation tests, global.json, Directory.Build.props and the Forge starter match the preserved original commit. No M1 application/test source was changed.
- Ignore checks confirm generated outputs and the added secret filename patterns are excluded. No existing tracked file matches the ignore rules.

Commands use the isolated SDK executable described above. The test result is for the separated M1 solution only; it does not validate the preserved M2 draft.

The original mixed commit's hosted workflow failed at dependency restore; build and tests were skipped. Local validation does not establish hosted CI success. The cleaned baseline requires a new hosted run after an explicitly approved push.

## Secret and artifact exclusions

Build outputs, `.artifacts/`, test reports and `.env` files remain ignored. Additional exclusions cover PEM/P8 private-key files, secrets/credentials JSON, development appsettings and the identified database-password filename. Ignore rules do not sanitize existing Git history and cannot identify every possible secret filename.

## Review boundary

No remote refs are changed. Do not resume M2, restore its dependencies, create resources or push either branch until the owner explicitly approves. Legacy v1 evaluation deserialization and application-level authorization/persistence remain deferred as documented in the M1 sign-off.
