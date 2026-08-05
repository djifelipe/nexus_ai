## ADDED Requirements

### Requirement: Closed read-only tool catalog
The system SHALL register exactly the Phase 3 tools `inventory.getBalance`, `invoice.getStatus`, `permission.check`, `workflow.get`, and `customer.getSummary` as `ReadOnly`, with fixed JSON Schemas and required permissions, and MUST reject every unregistered or write-capable tool.

#### Scenario: Registered tool is resolved
- **WHEN** the model requests a Phase 3 tool by its exact registered name
- **THEN** the executor resolves its immutable definition and validates the arguments against its schema

#### Scenario: Unknown or write tool is requested
- **WHEN** the model requests an unregistered name or a write operation
- **THEN** the executor returns `tool_not_registered` without invoking an MCP server

### Requirement: Authenticated identity and deterministic authorization
The executor MUST derive effective company, user, and permissions from authenticated context, MUST reject conflicting identity arguments, and SHALL authorize every invocation before external access.

#### Scenario: Authorized same-tenant request
- **WHEN** a registered tool has valid arguments and the authenticated user has its required permission for the effective company
- **THEN** the executor invokes the handler with that authenticated scope

#### Scenario: Cross-tenant argument is supplied
- **WHEN** tool arguments contain a company identifier different from authenticated context
- **THEN** the executor returns `access_denied` and no MCP operation occurs

#### Scenario: Required permission is absent
- **WHEN** the authenticated user lacks a permission required by the tool definition
- **THEN** the executor returns `access_denied` without revealing which protected data exists

### Requirement: Source-specific MCP access
The system MUST execute inventory, invoice status, permission, and customer summary reads exclusively through `supabase-mcp-server_ts`, and MUST execute workflow reads exclusively through `supabase-mcp-server_kb`. The application MUST NOT open direct database connections, expose MCP credentials, accept arbitrary SQL, or combine both servers inside one tool invocation.

#### Scenario: ERP data tool succeeds
- **WHEN** an authorized request invokes `inventory.getBalance`, `invoice.getStatus`, `permission.check`, or `customer.getSummary`
- **THEN** only the allowlisted operation on `supabase-mcp-server_ts` is used with the authenticated company scope

#### Scenario: Workflow tool succeeds
- **WHEN** an authorized request invokes `workflow.get` with catalog-valid module, feature, and action
- **THEN** only `supabase-mcp-server_kb` is queried and the result contains an authorized published workflow, version, and traceable source identifier

### Requirement: Minimal functional results
Each tool SHALL return only the minimum allowlisted fields needed for its purpose: product balance and unit; document status and non-sensitive status details; permission decision and safe scope; published workflow steps/version/source; or a summarized customer profile. Results MUST exclude credentials, internal SQL, stack traces, banking details, complete fiscal documents, and unnecessary personal data.

#### Scenario: Customer summary contains sensitive source fields
- **WHEN** the ERP response includes fields outside the customer summary allowlist
- **THEN** those fields are removed before the result is available to the model or audit sink

#### Scenario: Sanitization cannot establish a safe result
- **WHEN** a handler cannot map or sanitize an external result into its allowlisted contract
- **THEN** the executor returns `result_rejected` and does not provide the raw result to the model

### Requirement: Execution limits and safe failures
The system MUST enforce at most five tool calls per chat request, at most two calls of the same tool, a ten-second timeout per call, and propagated client cancellation. It SHALL map invalid arguments, denial, absence, timeout, and dependency failure to stable sanitized codes.

#### Scenario: Repetition limit is reached
- **WHEN** a model requests the same tool a third time in one chat request
- **THEN** the orchestrator stops that execution path with a controlled limit error and makes no third MCP call

#### Scenario: MCP call times out
- **WHEN** an allowed MCP operation does not finish within ten seconds
- **THEN** it is cancelled and returned as `timeout` without raw dependency details

#### Scenario: External record is absent
- **WHEN** a valid authorized lookup finds no matching record
- **THEN** the tool returns a sanitized `not_found` result rather than fabricating data

