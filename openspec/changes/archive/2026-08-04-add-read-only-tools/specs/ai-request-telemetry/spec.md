## ADDED Requirements

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

