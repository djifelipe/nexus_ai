## ADDED Requirements

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
The orchestrator SHALL execute intent routing, retrieval, prompt building, Ollama generation, citation validation, and telemetry in order and SHALL propagate cancellation and correlation identifiers through every stage.

#### Scenario: Client cancels processing
- **WHEN** the request cancellation token is triggered
- **THEN** the system cancels pending downstream work and records the operation as cancelled without returning an internal stack trace

#### Scenario: Model requests a tool
- **WHEN** Ollama returns a tool request during Phase 1
- **THEN** the system refuses execution and returns a controlled unsupported-operation result because no tools, especially write tools, are enabled

### Requirement: Safe failure contract
The API MUST translate validation, access, insufficient-knowledge, timeout, and external-service failures into stable safe responses without exposing credentials, SQL, prompts, or stack traces.

#### Scenario: External dependency fails
- **WHEN** PostgreSQL, the embedding provider, or Ollama fails or times out
- **THEN** the API returns the mapped safe error/status with its request ID and records a sanitized error code
