# Phase 3 acceptance record

Date: 2026-08-04

## Deferred live baseline decision

The integrated latency and error-rate baseline is formally deferred to a later operational validation because the currently available hardware is resource-constrained and would not produce a representative capacity baseline. This deferral does not waive the test: it remains a mandatory pre-enablement gate for each environment and tenant.

Phase 3 is considered implementation-complete based on its automated acceptance coverage. Until the deferred validation is performed:

- `ReadOnlyTools:Enabled` must remain empty in shared and production environments;
- no performance or SLA claim may be inferred from local test duration;
- each MCP operation, permission mapping, customer allowlist, p50/p95/p99 latency, and error rate must be verified on representative hardware before activation;
- write-capable tools remain prohibited independently of this deferral.

## Automated verification

- Application build: passed with zero warnings and zero errors.
- Test suite: 47 passed, 0 failed, 0 skipped.
- Scoped formatting verification: passed for all Phase 3 source files.
- Architecture verification: application artifacts remain under `AiGateway`; Domain/Application do not depend on API/Infrastructure; no direct `DbConnection`/`NpgsqlConnection` usage exists.
- Write-tool verification: the catalog contains exactly five read-only names and rejects unknown names such as `invoice.cancel` before MCP access.

The automated suite covers exact and disabled catalog resolution, JSON argument validation, authenticated identity conflicts, tenant and permission denial, cancellation, per-call timeout, dependency and not-found mapping, unsafe result rejection, all five successful handlers, designated MCP routing, SQL-shaped operation rejection, Ollama tool-call parsing, untrusted tool-result prompting, citations, global and per-tool loop limits, non-blocking telemetry, and API regression behavior.

## Local baseline

The in-memory Phase 3 suite completes inside the one-second test runner resolution on the current development machine. Unit-level tool execution is intentionally not promoted as a production latency baseline because it excludes MCP transport and ERP workload.

Production tools remain disabled by default. Before enabling each tool in an environment, record the following from `ai_tool_duration_ms` and `ai_tool_calls_total` during a scoped smoke test:

| Tool | p50 | p95 | p99 | Error rate | Status |
|---|---:|---:|---:|---:|---|
| `inventory.getBalance` | pending live MCP | pending | pending | pending | disabled |
| `invoice.getStatus` | pending live MCP | pending | pending | pending | disabled |
| `permission.check` | pending live MCP | pending | pending | pending | disabled |
| `workflow.get` | pending live MCP | pending | pending | pending | disabled |
| `customer.getSummary` | pending live MCP | pending | pending | pending | disabled |

No tool may be enabled while its live baseline or permission/allowlist review remains pending. This table is the handoff record for the postponed validation and must be completed in a future operational change before rollout.
