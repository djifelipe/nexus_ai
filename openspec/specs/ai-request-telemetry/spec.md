# AI Request Telemetry Specification

## Purpose
Define correlated, sanitized, and non-blocking observability for Phase 1 chat requests.

## Requirements

### Requirement: Correlated request telemetry
Every chat request SHALL have a request ID and trace ID and SHALL propagate its conversation ID, effective company, and effective user through structured operation scopes.

#### Scenario: Request enters the pipeline
- **WHEN** an authenticated chat request is accepted
- **THEN** all component events can be correlated to the same request and trace without using user or company identifiers as unbounded metric labels

### Requirement: Stage latency and token metrics
Telemetry SHALL measure total, intent, retrieval, prompt, model, and validation durations and SHALL record prompt, completion, and context token counts when available.

#### Scenario: Grounded response completes
- **WHEN** the pipeline returns a grounded response
- **THEN** latency metrics for every executed stage, token availability, status, model, intent module, and source-use metadata are recorded

### Requirement: Sanitized observability
Logs and telemetry MUST exclude credentials, tokens, connection strings, internal prompts, stack traces, cross-tenant content, and unnecessary personal, banking, or fiscal data through allowlisted structured fields and sensitive-data sanitization.

#### Scenario: External error contains a secret
- **WHEN** a dependency error message contains a credential-like value
- **THEN** the stored event contains a sanitized error code and no secret value or raw stack trace

### Requirement: Non-blocking telemetry
Telemetry failure MUST NOT fail or materially alter an otherwise valid chat response.

#### Scenario: Telemetry sink unavailable
- **WHEN** the observability backend rejects an event during a successful request
- **THEN** the request continues to its normal response and the sink failure is handled locally without recursive logging failure

### Requirement: Correlated tool execution telemetry
Every attempted tool execution SHALL emit a correlated `ai.tool.execute` span and sanitized count/duration telemetry containing the registered tool name, risk level, success state, and stable error code when applicable. Company and user identifiers MUST NOT be metric labels.

#### Scenario: Tool execution completes
- **WHEN** a registered read-only tool succeeds or fails
- **THEN** its event is correlated with request, trace, and conversation and includes duration and outcome without raw arguments or result payloads

### Requirement: Sanitized tool audit
The system MUST audit tool name, authenticated company and user, read-only risk, duration, success, and stable error code using allowlisted fields, while excluding credentials, internal prompts, SQL, stack traces, complete documents, banking or fiscal data, and unnecessary personal data.

#### Scenario: External response contains sensitive data
- **WHEN** a tool dependency returns sensitive or unexpected fields
- **THEN** neither those fields nor the raw payload are persisted in logs, spans, metrics, or audit records

### Requirement: Non-blocking tool telemetry
Failure of a telemetry or audit sink MUST NOT fail or materially change an otherwise valid tool result or chat response.

#### Scenario: Audit sink is unavailable
- **WHEN** a read-only tool succeeds but its audit sink is unavailable
- **THEN** orchestration continues with the sanitized result and handles the sink failure locally
