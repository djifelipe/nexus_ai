## 1. Domain contracts and catalog

- [x] 1.1 Add `Domain/Tools` read-only definitions, execution request/result envelopes, stable error codes, and tool risk model under `AiGateway`.
- [x] 1.2 Define fixed JSON Schemas, required permissions, and allowlisted result contracts for the five Phase 3 tool names.
- [x] 1.3 Implement an immutable `Application/Tools` registry that resolves only enabled registered definitions and rejects unknown or write-capable names.
- [x] 1.4 Add unit tests for exact-name resolution, disabled tools, schema metadata, and rejection of unknown/write tools.

## 2. Execution security pipeline

- [x] 2.1 Implement JSON Schema argument validation and stable `invalid_arguments` mapping before any external access.
- [x] 2.2 Implement authenticated company/user binding and rejection of conflicting identity arguments.
- [x] 2.3 Implement deterministic required-permission checks and tenant-scoped authorization before handler dispatch.
- [x] 2.4 Add centralized timeout, cancellation, sanitized error mapping, and result allowlist/sanitization to `IToolExecutor`.
- [x] 2.5 Add unit tests proving invalid, cross-tenant, unauthorized, cancelled, timed-out, and unsafe-result requests never leak data or invoke an MCP operation when rejected early.

## 3. MCP ports and adapters

- [x] 3.1 Define Application ports and typed queries/results for ERP reads and knowledge workflow reads without exposing SQL or MCP implementation types.
- [x] 3.2 Implement Infrastructure adapters that route inventory, invoice status, permission, and customer summary only through `supabase-mcp-server_ts` allowlisted operations.
- [x] 3.3 Implement the Infrastructure workflow adapter using only `supabase-mcp-server_kb`, enforcing catalog identifiers, publication, version, tenant, and permission filters.
- [x] 3.4 Configure MCP adapter registration, endpoints, credentials, and per-tool feature flags without logging secrets or opening direct database connections.
- [x] 3.5 Add contract/integration tests with MCP doubles proving the correct server is selected, arbitrary SQL is impossible, scopes are propagated, and dependency errors are sanitized.

## 4. Phase 3 tool handlers

- [x] 4.1 Implement `inventory.getBalance` with product and optional establishment/warehouse inputs and minimal balance/unit output.
- [x] 4.2 Implement `invoice.getStatus` with document type/identifier inputs and minimal status, date, and safe-reason output.
- [x] 4.3 Implement `permission.check` with permission-code input and non-disclosing decision/scope output.
- [x] 4.4 Implement `workflow.get` with catalog-valid module/feature/action inputs and published steps, version, and source identifier output.
- [x] 4.5 Implement `customer.getSummary` with customer identifier input and a configurable, version-aware allowlist excluding full fiscal, banking, and unnecessary personal data.
- [x] 4.6 Add handler tests for successful, not-found, permission-denied, malformed, cross-tenant, and sensitive-source-result scenarios for all five tools.

## 5. Intent and Ollama integration

- [x] 5.1 Extend intent routing rules to emit only enabled catalog names in `RequiredTools` for data query, permission, and workflow intents, preserving clarification for ambiguity.
- [x] 5.2 Extend the Ollama client contracts and prompt/tool definition mapping to receive structured tool calls and return sanitized tool results as untrusted data.
- [x] 5.3 Add tests for exact required-tool selection, disabled-tool fallback, ambiguous intent, tool-call parsing, and prompt-injection content in tool results.

## 6. Orchestration and limits

- [x] 6.1 Integrate `IToolExecutor` into `AiOrchestrator` between Ollama calls while preserving correlation identifiers and final response validation.
- [x] 6.2 Enforce five total tool executions, two repetitions per tool, ten-second per-call timeout, client cancellation, and controlled loop termination.
- [x] 6.3 Preserve explicit rejection for every unknown or write-capable tool and map tool failures to stable safe chat warnings/statuses.
- [x] 6.4 Add orchestration tests for one and multiple successful calls, prohibited calls, repetition/global limits, timeouts, cancellation, dependency failure, and final model response.

## 7. Audit and observability

- [x] 7.1 Add correlated `ai.tool.execute` spans and bounded count/duration metrics with tool name, risk, outcome, and stable error code.
- [x] 7.2 Implement sanitized tool audit records containing authenticated identity and execution metadata without raw arguments, raw results, or sensitive fields.
- [x] 7.3 Make telemetry/audit sink failures non-blocking and add tests for redaction, bounded metric labels, correlation, and sink unavailability.

## 8. Verification and rollout

- [x] 8.1 Add architecture tests verifying all application artifacts remain under `AiGateway`, layer dependencies follow the topic-11 structure, and direct database access is absent.
- [x] 8.2 Document per-environment MCP operation mappings, permission codes, customer-summary field allowlists, feature-flag rollout, and rollback under `AiGateway`.
- [x] 8.3 Run formatting, build, unit tests, integration tests, and security/tenant isolation tests; resolve all failures.
- [x] 8.4 Verify every Phase 3 acceptance scenario, confirm write tools remain unavailable, and formally defer live latency/error baselines due to limited hardware, retaining them as a mandatory pre-enablement gate by environment and tenant.
