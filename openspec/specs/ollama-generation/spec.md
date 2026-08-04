# Ollama Generation Specification

## Purpose
Define reliable, observable text generation through the configured Ollama model.

## Requirements

### Requirement: Configured Ollama generation
The system SHALL call a configured Ollama endpoint and model through an application port, pass only the constructed prompt package, and return generated content plus token usage when supplied by Ollama.

#### Scenario: Successful generation
- **WHEN** Ollama returns a valid response for an authorized prompt
- **THEN** the client maps content, finish information, and available prompt/completion token counts to the application contract

### Requirement: Resilient external call
The Ollama client MUST honor cancellation and configured timeout, validate response shape, and map network, timeout, and malformed-response failures to stable sanitized error codes.

#### Scenario: Ollama times out
- **WHEN** Ollama does not respond before the configured deadline
- **THEN** the call is cancelled and the pipeline returns a safe external-timeout response without retrying beyond the total request SLA

#### Scenario: Ollama returns malformed content
- **WHEN** the response cannot be parsed into the expected contract
- **THEN** it is rejected as an external-format error and raw sensitive payload data is not returned to the caller

### Requirement: Model latency measurement
The integration SHALL measure model duration and first-token latency when the selected Ollama protocol exposes streaming events.

#### Scenario: Streaming metrics available
- **WHEN** Ollama exposes the first response token event
- **THEN** telemetry records first-token latency and evaluates it against the 3-second target
