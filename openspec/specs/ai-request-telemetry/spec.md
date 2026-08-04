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
