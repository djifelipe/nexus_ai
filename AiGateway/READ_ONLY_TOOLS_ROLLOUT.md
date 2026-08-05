# Phase 3 read-only tools rollout

## Safety baseline

All tools are disabled by default through `ReadOnlyTools:Enabled`. Enable only exact names from this closed list:

- `inventory.getBalance`
- `invoice.getStatus`
- `permission.check`
- `workflow.get`
- `customer.getSummary`

Write operations, arbitrary tool names, direct database connections, and SQL-shaped MCP operations are not supported. Tenant and user always come from authenticated request context.

## MCP operation mappings

ERP reads use only `Mcp:Erp:ServerName=supabase-mcp-server_ts`:

| Gateway tool | Configuration key | Default MCP operation |
|---|---|---|
| `inventory.getBalance` | `Mcp:Erp:InventoryOperation` | `inventory_get_balance` |
| `invoice.getStatus` | `Mcp:Erp:InvoiceOperation` | `invoice_get_status` |
| `permission.check` | `Mcp:Erp:PermissionOperation` | `permission_check` |
| `customer.getSummary` | `Mcp:Erp:CustomerOperation` | `customer_get_summary` |

Workflow reads use only `Mcp:WorkflowTools:ServerName=supabase-mcp-server_kb` and `Mcp:WorkflowTools:Operation=workflow_get`. Deployments must verify these named operations exist on their MCP servers before enabling a tool. Operation names containing `sql` are rejected.

Credentials are supplied only through the configured environment-variable names (`SUPABASE_ACCESS_TOKEN_TS` for ERP and `SUPABASE_ACCESS_TOKEN` for knowledge). Never put credential values in appsettings.

## Permission mapping

| Tool | Required gateway permission |
|---|---|
| `inventory.getBalance` | `Inventory.Balance.View` |
| `invoice.getStatus` | `Invoice.Status.View` |
| `permission.check` | `Security.Permission.View` |
| `workflow.get` | `Knowledge.Workflow.View` |
| `customer.getSummary` | `Customer.Summary.View` |

Each MCP operation must enforce the same effective company scope as defense in depth. Permission decisions are evaluated again for every call.

## Customer summary allowlist

`ReadOnlyTools:CustomerSummaryAllowedFields` controls optional fields accepted from ERP. The fixed result already includes `customerId`, `displayName`, `status`, `city`, and `state`. Do not add banking details, full fiscal documents, credentials, tokens, or unnecessary personal data. Validate the allowlist for every supported ERP version before rollout.

## Progressive enablement

> Deferred gate (2026-08-04): live performance/error baselines were postponed because the current hardware is limited and non-representative. The implementation phase is complete, but this section must be executed on representative hardware before any shared or production enablement.

1. Keep `Enabled` empty while validating configuration and MCP health in the target environment.
2. Enable one exact tool for an internal tenant and verify access-denied, cross-tenant, not-found, timeout, and sanitization cases.
3. Inspect `ai.tool.execute`, `ai_tool_calls_total`, and `ai_tool_duration_ms` using bounded tool/outcome/error dimensions. User and company IDs must not be metric labels.
4. Record p50/p95/p99 latency and error rate for each enabled tool before expanding tenants.
5. Enable remaining tools independently; never enable a write operation.

The executor limits a request to five calls, two calls per tool, and ten seconds per call. The overall chat timeout must remain greater than a single tool timeout.

## Rollback

Set `ReadOnlyTools:Enabled` to an empty array and redeploy/reload configuration. This returns the gateway to knowledge-only behavior and requires no data migration. If one MCP operation is unhealthy, remove only its exact gateway tool from the enabled list.
