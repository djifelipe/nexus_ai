# Citation Validation Specification

## Purpose
Ensure generated factual answers remain grounded in the exact authorized knowledge supplied to the model.

## Requirements

### Requirement: Citation membership validation
The validator MUST extract citations in `[source-id]` format and accept as valid only IDs from the exact knowledge set sent to Ollama.

#### Scenario: All citations are known
- **WHEN** a generated factual answer cites only source IDs present in the prompt package
- **THEN** citation validation succeeds and the cited sources are included in the API response

#### Scenario: Answer invents a citation
- **WHEN** the generated answer cites an ID absent from the prompt package
- **THEN** the answer is not returned as grounded and the invalid identifier is recorded as a sanitized validation reason

### Requirement: Insufficient-knowledge response
The validator SHALL return `InsufficientKnowledge` with a safe standard answer when no authorized sources exist or a factual answer has no required valid citation.

#### Scenario: Retrieval returns no source
- **WHEN** retrieval completes successfully with no authorized knowledge
- **THEN** the model is not asked to invent an answer and the API states that there is insufficient knowledge to answer safely

#### Scenario: Factual response omits citations
- **WHEN** Ollama produces a factual response from supplied knowledge without a valid citation
- **THEN** the validator rejects grounded status and returns the configured insufficient-knowledge or review result

### Requirement: Basic-validation latency
Deterministic citation validation SHALL complete within 300 ms under the defined acceptance-test workload.

#### Scenario: Maximum response validation
- **WHEN** a response at the configured maximum size is checked against the maximum source set
- **THEN** validation meets the 300 ms target
