namespace TaskMetricAI.Evaluation;

/// <summary>Optional starter definitions only. Never invoked by the evaluator; no weights or activation.</summary>
public static class StandardMetricTemplate
{
    public static MetricCatalog CreateCatalog(string tenantId)
    {
        MetricDefinition Metric(string id, string label, string description, ScoreAnchors anchors) =>
            new(tenantId, id, 1, MetricKind.Standard, label, description, anchors);

        return new(tenantId,
        [
            Metric("implementation-complexity", "Implementation complexity", "Complexity of algorithms, branching and business rules.",
                new("Known isolated rule with no branching change", "Localized rule change using an existing pattern",
                    "Several interacting paths or business rules", "Broad algorithmic or rule interactions",
                    "Cross-cutting algorithms with substantial coordination")),
            Metric("codebase-impact", "Codebase impact", "Breadth of affected modules, layers and consumers.",
                new("Single isolated symbol", "Small set of files within one module", "Multiple layers within one bounded feature",
                    "Multiple modules or shared consumers", "Cross-cutting public contracts across the codebase")),
            Metric("integration-complexity", "Integration complexity", "External contracts and cross-system coordination.",
                new("No integration changes", "One existing integration with a known contract",
                    "Moderate changes across an existing integration", "Multiple integrations or significant retry behavior",
                    "Cross-system coordination with substantial reliability or contract risk")),
            Metric("data-complexity", "Data complexity", "Schema, transactions, migrations and consistency requirements.",
                new("No persistent data changes", "Localized field or query using an existing pattern",
                    "Related entities or a bounded migration", "Complex transactions or coordinated backfill",
                    "Cross-system consistency and high-risk data migration")),
            Metric("testing-complexity", "Testing complexity", "Test setup, negative paths and regression verification.",
                new("One isolated unit assertion", "Several local unit and negative cases",
                    "Integration fixtures and feature regression cases", "Concurrency or multi-service failure testing",
                    "Broad end-to-end reliability and migration verification")),
            Metric("requirements-uncertainty", "Requirements uncertainty", "Explicit unresolved requirements and dependency decisions.",
                new("Acceptance criteria and dependencies are confirmed", "Minor bounded clarification remains",
                    "Several material decisions require confirmation", "Major dependency or behavior decisions remain open",
                    "Known fundamental scope decisions require substantial investigation")),
            Metric("security-reliability", "Security and reliability", "Authorization, concurrency and resilience changes.",
                new("No security or reliability behavior changes", "Local validation using an established control",
                    "Feature-level authorization or resilience handling", "Multiple trust boundaries or concurrent failure paths",
                    "Cross-system trust and substantial recovery guarantees")),
            Metric("deployment-complexity", "Deployment complexity", "Configuration, rollout and infrastructure coordination.",
                new("Existing release process without configuration changes", "One bounded configuration change",
                    "Coordinated configuration or schema rollout", "Staged rollout and rollback across components",
                    "High-risk infrastructure transition with coordinated recovery"))
        ]);
    }
}
