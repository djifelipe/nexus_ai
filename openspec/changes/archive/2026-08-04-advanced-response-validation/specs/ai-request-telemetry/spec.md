## ADDED Requirements

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

