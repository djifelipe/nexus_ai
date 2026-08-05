# AI Chat Orchestration Specification

## Purpose
Define the authenticated Phase 1 chat API, its deterministic grounded-answer pipeline, and safe failure behavior.

## Requirements

### Requirement: Application project root
All application source code, configuration, migrations, tests, and operational documentation MUST be located under `C:\github.com\djifelipe\nexus_ai\AiGateway`. The `openspec` directory at repository root SHALL contain planning artifacts only.

#### Scenario: Implementation creates an application artifact
- **WHEN** a Phase 1 task creates or modifies source code, configuration, a migration, a test, or application documentation
- **THEN** the artifact path is inside `C:\github.com\djifelipe\nexus_ai\AiGateway` and the topic-11 directories are resolved relative to that root

### Requirement: Authenticated chat endpoint
The system SHALL expose `POST /api/ai/chat`, validate its required contract, and derive the effective company and user from the authenticated context.

#### Scenario: Grounded request succeeds
- **WHEN** an authenticated user submits a valid message whose company and user match the authenticated context
- **THEN** the system returns a request ID, conversation ID, answer, status, confidence, intent, sources, warnings, and latency metrics

#### Scenario: Payload identity conflicts with authentication
- **WHEN** a payload company or user differs from the authenticated context
- **THEN** the system denies the request before intent routing or knowledge retrieval

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

### Requirement: Safe failure contract
The API MUST translate validation, access, insufficient-knowledge, timeout, and external-service failures into stable safe responses without exposing credentials, SQL, prompts, or stack traces.

#### Scenario: External dependency fails
- **WHEN** PostgreSQL, the embedding provider, or Ollama fails or times out
- **THEN** the API returns the mapped safe error/status with its request ID and records a sanitized error code
