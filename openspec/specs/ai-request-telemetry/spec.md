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

### Requirement: Advanced-validation telemetry
Every advanced validation attempt SHALL emit a correlated `ai.response.validate` span and bounded metrics for duration, final status, grounding-score band, claim count, supported-claim count, unsupported-claim count, citation coverage, semantic-check outcome, policy version, and whether regeneration occurred.

#### Scenario: Advanced validation completes
- **WHEN** a generated or regenerated response reaches a validation decision
- **THEN** the attempt is correlated to the request and trace with bounded aggregate fields and no raw answer, claim, evidence, prompt, company, or user value as a metric label

#### Scenario: External semantic check fails
- **WHEN** an embedding or evaluator dependency times out or fails
- **THEN** telemetry records a sanitized stable dependency code and duration without exception text, raw payload, prompt, or stack trace

### Requirement: Regeneration telemetry
The system MUST record the initial validation status, regeneration trigger category, attempt count capped at one, and terminal validation status without persisting validation feedback text or generated content.

#### Scenario: One regeneration is performed
- **WHEN** a correctable response triggers automatic regeneration
- **THEN** correlated telemetry distinguishes the initial and terminal validation attempts and records exactly one regeneration

#### Scenario: Unsafe response is blocked
- **WHEN** a security or permission failure prevents regeneration
- **THEN** telemetry records a bounded sanitized category and zero regeneration attempts without storing the detected value

### Requirement: Non-blocking advanced-validation observability
Failure of the telemetry sink MUST NOT change an advanced-validation decision, trigger regeneration, or fail an otherwise valid chat response.

#### Scenario: Telemetry sink fails after grounded decision
- **WHEN** the observability backend rejects the advanced-validation event
- **THEN** the grounded decision and response remain unchanged and the sink failure is handled locally without recursive logging
