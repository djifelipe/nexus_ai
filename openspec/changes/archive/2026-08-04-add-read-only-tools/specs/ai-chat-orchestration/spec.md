## MODIFIED Requirements

### Requirement: Deterministic phase-one pipeline
The orchestrator SHALL execute intent routing, retrieval, prompt building, Ollama generation, authorized Phase 3 read-only tool execution when requested, citation validation, and telemetry in order and SHALL propagate cancellation and correlation identifiers through every stage. Tool results MUST be attached as untrusted data for a subsequent model call, and the orchestration loop MUST observe the configured global and per-tool limits.

#### Scenario: Client cancels processing
- **WHEN** the request cancellation token is triggered
- **THEN** the system cancels pending downstream work, including MCP tool operations, and records the operation as cancelled without returning an internal stack trace

#### Scenario: Model requests an authorized read-only tool
- **WHEN** Ollama returns a valid call for a registered Phase 3 tool within execution limits
- **THEN** the orchestrator executes it through `IToolExecutor`, supplies the sanitized result to Ollama, and continues until a final response or controlled terminal condition

#### Scenario: Model requests a prohibited tool
- **WHEN** Ollama requests an unknown or write-capable tool
- **THEN** the system refuses execution and returns a controlled unsupported-operation result without reinterpreting the requested name

#### Scenario: Tool loop reaches a limit
- **WHEN** tool calls reach five total executions or would repeat one tool more than twice
- **THEN** the orchestrator terminates the loop with a safe stable warning and does not perform the excess call

