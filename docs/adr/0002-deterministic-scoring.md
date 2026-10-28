# ADR 0002: Decimal precision, immutable snapshots and sensitivity

Date: 27 October 2026
Status: Accepted implementation rules for approved docs/12

## Validation and identity

28 October extension: ADR 0004 supersedes v1 canonical identity for newly constructed profiles with `profile-content-v2`, adding mandatory tenant ID, criterion enabled state and optional catalog provenance. Hashes/schema of historical v1 content must never be reinterpreted. Validate one assessment per **enabled** metric and sum **enabled** weights exactly 1. Disabled definitions still require valid IDs/rubrics/ranges, but are excluded from evaluation and sensitivity; enabled zero-weight metrics keep previous semantics. All remaining precision/unknown/interval/sensitivity rules below apply to enabled metrics.

Scoring policy `wsm-v1`; sensitivity policy `relative-10-v1`. Reject unsupported versions. All weights are decimal fractions [0,1], enabled weights sum exactly 1. Profile-configured `PercentageDecimalPlaces` accepts 0–4 (default 2), so weight values have at most 2–6 effective fractional digits. Trailing decimal zeros do not change validity or identity. Never round or normalize invalid configured values.

Criterion scores are nullable integers 1–5; null is explicit unknown. Require one assessment per enabled ordinal/case-sensitive criterion ID; reject duplicates, omissions, disabled IDs and extras. All five nonempty criterion-specific anchors are required, including for disabled entries. Complexity bands and optional point bands independently cover [1,5], with positive width and exactly one inclusive side at every shared boundary; domain endpoints must be inclusive. Enabled empty mappings are invalid; null disables points. Point candidates must be positive integers. Classification uses unrounded score. No mandatory production metric set, rubric, labels, weights or team mapping is built into scoring. The optional standard template is separate data.

Collections are defensively copied into read-only snapshots. Current canonical content is compact UTF-8 JSON with fixed field order under schema `profile-content-v2` (ADR 0004). Historical v1 content retains its original schema/hash. Input criterion and interval order is significant, rubric order is 1–5. IDs use ordinal comparison. Decimals are invariant `G29` strings, excluding insignificant trailing zeros. Hash covers tenant/profile ID/version, name/description, scope, embedded criteria/rubrics/weights/enabled state/catalog provenance, bands, optional mapping, precision and policy versions. Hash is lowercase SHA-256; excludes lifecycle status and audit metadata (creator/approver/timestamps/change reason). Metadata remains in the snapshot contract for future persistence. Strings are hashed exactly, without trimming or Unicode normalization. JSON null mapping differs from enabled-empty mapping (invalid). A hash proves content identity, not administrator authorization. Canonical JSON is immutable content serialization; mutable lifecycle is stored separately from M2.

## Unknowns and mathematics

S = sum(weight × score), N = 25 × (S − 1), decimal arithmetic. Return each contribution. Positive-weight unknowns produce score/index bounds substituting 1/5 and no definitive score/class/points. Preserve missing questions. Zero-weight unknowns remain listed with contribution 0 and do not prevent definitive scoring. No confidence inference, hours or reweighting. Reject profile identity/version/hash mismatches before evaluation.

## Hypothetical sensitivity only

For every enabled criterion, attempt -10% and +10% relative perturbations. Disabled criteria are excluded entirely. Zero target weight: NotApplicable (no relative change). Sole positive criterion: NotApplicable (no other positive recipient). Target above 1: NotApplicable (outside domain). No invalid scenario is clamped or silently repaired.

Use 12 fractional weight digits, `U = 10^12` units. Valid configured weights (at most 6 fractional digits) and their ±10% targets are exact in these units. Preserve the target exactly. For each other criterion j, allocate remaining units proportionally: quotient/remainder of `(oldUnits[j] × remainingUnits) / oldRemainingUnits`, calculated with BigInteger. Start with floor quotients; allocate leftover units to descending exact remainder, ties by ordinal criterion ID. Zero-weight recipients remain zero. The sum is exactly U and all scenario weights are nonnegative. No floating point or rounding-mode dependence. Recompute results against hypothetical weights while retaining original profile identity and labeling scenarios as hypothetical, never an activated profile version.

Report changed band/points only for definitive comparisons. Incomplete scenarios return provisional bounds; stability is unknown (null). Degenerate scenarios carry an explicit reason and no result. Configured input precision is separate from sensitivity's 12-digit arithmetic precision. Stable ordering and pure evaluation support concurrency and culture independence.
