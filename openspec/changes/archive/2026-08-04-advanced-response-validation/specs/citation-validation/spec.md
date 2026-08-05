## MODIFIED Requirements

### Requirement: Citation membership validation
The validator MUST extract citations in `[source-id]` format, accept as valid only IDs from the exact authorized knowledge set sent to Ollama, and associate valid citations with the claims they are capable of supporting.

#### Scenario: All citations support their claims
- **WHEN** a generated factual answer cites only source IDs present in the prompt package and each material claim is supported by its cited source
- **THEN** citation validation succeeds and the cited sources are included in the API response

#### Scenario: Answer invents a citation
- **WHEN** the generated answer cites an ID absent from the prompt package
- **THEN** the answer is not returned as grounded and the invalid identifier is recorded as a sanitized validation reason

#### Scenario: Valid citation does not support claim
- **WHEN** a cited source ID is present in the prompt package but its content does not support the associated material claim
- **THEN** that claim is marked unsupported and citation membership alone does not make the answer grounded

### Requirement: Insufficient-knowledge response
The validator SHALL return `InsufficientKnowledge` with a safe standard answer when no authorized sources exist, a factual answer has no required valid citation, or authorized evidence cannot support its material claims after advanced validation.

#### Scenario: Retrieval returns no source
- **WHEN** retrieval completes successfully with no authorized knowledge
- **THEN** the model is not asked to invent an answer and the API states that there is insufficient knowledge to answer safely

#### Scenario: Factual response omits citations
- **WHEN** Ollama produces a factual response from supplied knowledge without a valid citation
- **THEN** the validator rejects grounded status and returns the configured insufficient-knowledge or review result

#### Scenario: Cited knowledge is insufficient
- **WHEN** citations are valid members of the prompt package but do not support one or more material claims
- **THEN** the validator removes unsupported content through controlled regeneration or returns the safe insufficient-knowledge result

