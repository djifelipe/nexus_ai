## ADDED Requirements

### Requirement: Verifiable claim extraction
The validator SHALL extract bounded, individually identifiable factual claims from a generated answer while preserving the original answer unchanged for final decision-making.

#### Scenario: Answer contains multiple factual statements
- **WHEN** a generated answer contains multiple procedural or business-rule statements
- **THEN** the validator produces stable claim identifiers and the text span for each verifiable claim before grounding evaluation

#### Scenario: Claim extractor is ambiguous
- **WHEN** the optional model-based extractor fails, times out, or returns invalid structured output
- **THEN** the validator uses deterministic extraction and returns `RequiresReview` when safe extraction coverage cannot be established

### Requirement: Authorized semantic grounding
The validator MUST compare each claim only against traceable sources that were authorized for the authenticated tenant, ERP version, permissions, publication state, and included in the exact prompt package. A model evaluator MUST NOT be the sole grounding mechanism.

#### Scenario: Every material claim is supported
- **WHEN** every material claim has a valid citation or semantic evidence in an authorized prompt source above the configured threshold
- **THEN** each claim records its supporting source IDs and is eligible for a grounded decision

#### Scenario: Claim conflicts with authorized evidence
- **WHEN** a claim states a fixed cancellation period but the authorized source states that the period depends on jurisdiction
- **THEN** the claim is marked unsupported or contradicted and the answer is not classified as `Grounded`

#### Scenario: Evidence belongs to another tenant or version
- **WHEN** semantically similar evidence is not part of the authorized prompt package for the effective tenant and ERP version
- **THEN** the validator excludes that evidence and records no cross-tenant content in its result or telemetry

#### Scenario: Semantic dependency fails
- **WHEN** an embedding or optional evaluator dependency fails or exceeds its timeout
- **THEN** the validator returns a conservative status with a sanitized stable error code and never approves the response solely because the check was unavailable

### Requirement: Deterministic grounding score
The validator SHALL calculate a normalized confidence score from retrieval coverage weighted 0.35, citation coverage weighted 0.25, semantic grounding weighted 0.25, and intent confidence weighted 0.15, using validated configurable thresholds. Mandatory security, permission, and citation failures MUST override the numeric score.

#### Scenario: Fully supported answer
- **WHEN** all score components are present, all mandatory checks pass, and the result meets the grounded threshold
- **THEN** the validator returns `Grounded` with the computed score and traceable supporting source IDs

#### Scenario: High score with permission violation
- **WHEN** the numeric score meets the grounded threshold but the answer contains instructions forbidden by the effective user's permissions
- **THEN** the validator returns `Unsafe` and does not allow the score to override the permission failure

#### Scenario: Knowledge is insufficient
- **WHEN** authorized evidence cannot support the material claims or no authorized source is available
- **THEN** the validator returns `InsufficientKnowledge` with the configured safe answer and no invented detail

### Requirement: Sensitive-data and policy enforcement
The validator MUST detect credentials, tokens, connection strings, internal prompts, SQL, stack traces, cross-tenant content, and unnecessary personal, banking, or fiscal data before release. Detected raw values MUST NOT appear in the client response, reasons, logs, spans, metrics, or audits.

#### Scenario: Generated answer exposes a secret
- **WHEN** a generated answer contains a credential-like value or internal connection string
- **THEN** the validator returns `Unsafe`, blocks the generated content, and emits only a category and sanitized reason code

#### Scenario: Answer suggests bypassing access control
- **WHEN** a generated answer instructs the user to bypass a permission or reveals data outside the authenticated scope
- **THEN** the validator returns `Unsafe` without regeneration and records a sanitized security event

#### Scenario: Legitimate masked identifier
- **WHEN** an allowlisted response field contains an identifier already masked according to policy
- **THEN** the detector does not expose its raw value and evaluates it according to the configured category policy

### Requirement: Bounded advanced validation
The validator MUST enforce configured limits for response size, claim count, evidence candidates, external-call timeout, and cancellation, and SHALL produce a stable safe result when a limit is exceeded.

#### Scenario: Client cancels validation
- **WHEN** the request cancellation token is triggered during claim or semantic validation
- **THEN** all pending validation work is cancelled and no partial answer is released as grounded

#### Scenario: Claim count exceeds limit
- **WHEN** extraction produces more claims than the validated configured maximum
- **THEN** the validator stops bounded processing and returns `RequiresReview` or another configured safe terminal status

