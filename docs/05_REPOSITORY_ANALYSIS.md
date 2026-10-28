# GitHub and .NET Repository Analysis Specification

## Inputs
Authorized GitHub installation/repository; selected commit SHA; Jira issue description, acceptance criteria and linked issue references. Repo selection by configured project/component mapping, optionally explicit issue mapping.

## Inspection strategy
1. Read repository tree, solution and csproj files; bound file count and size.
2. Extract API route/method, controller/action, minimal API definitions where supported, DTO shapes and validators.
3. Identify service/interface implementations, DI registrations, EF DbContext/entities/migrations, external client wrappers, background jobs, tests.
4. Use lexical matching and symbols to shortlist candidates; inspect direct dependencies and test references. Treat dynamic reflection and runtime configuration as limitations.
5. Return structured evidence records: repo, SHA, path, line start/end, symbol, observation, confidence category.

## Impact classification
Verified: direct code fact (method exists, endpoint route, test case). Inferred: likely impacted service based on matching names/dependencies. Recommended: suggested new implementation/test. Do not present inferred impact as verified.

## Example output
Ticket asks for payment callback idempotency. Evidence may reveal callback route, transaction entity and existing tests. Report potential duplicate processing risk only if evidence supports it; otherwise flag as an investigation question.

## Safety and privacy
Prefer GitHub API read-only retrieval; never execute untrusted code or CI scripts in MVP. Do not fetch arbitrary URL references from issue text. Strip secrets and PII from context; honor repository access and tenant boundaries. Do not send full repositories to an LLM. Enforce file-type, path, token and request budgets. Pin SHA for reproducibility.

## Limitations
Cannot reliably infer undocumented business intent from code; cannot guarantee exact change set before implementation; runtime behavior may depend on configuration not visible in repo. Record unsupported language/framework patterns.

## Testing
Fixture repositories: simple CRUD, payment callback with concurrency, multi-project clean architecture, missing tests, dynamic routes, huge repository, intentionally malicious prompt-injection comments. Assert evidence path/line integrity and no execution.

## v1.1 bounded orchestration
Repository inspection is invoked by the single evaluation workflow, with read-only credentials, pinned SHA, explicit budgets and no arbitrary code execution. Return typed evidence with verified/inferred status. Do not create autonomous agents that browse or modify repositories independently.
