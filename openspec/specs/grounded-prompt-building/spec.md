# Grounded Prompt Building Specification

## Purpose
Define safe, structured, and token-aware prompt construction from authorized knowledge.

## Requirements

### Requirement: Structured grounded prompt
The prompt builder SHALL separate system policy, authenticated user context, validated intent, identified knowledge sources, conversation summary, and the unchanged original question.

#### Scenario: Prompt built from authorized sources
- **WHEN** the builder receives authorized knowledge items
- **THEN** every item is delimited as data with its source ID and the original question is preserved verbatim

### Requirement: Token-aware prioritization
The builder MUST estimate tokens before generation, respect the configured model limit and response reserve, and prioritize critical rules, exact workflows, permissions, validations, exceptions, examples, FAQs, and complementary documentation in that order.

#### Scenario: Prompt exceeds model limit
- **WHEN** the candidate prompt exceeds its token budget
- **THEN** the builder removes lowest-priority sources first while preserving system rules, critical rules, and the complete original question

### Requirement: Prompt injection containment
Recovered text MUST be treated as untrusted data and SHALL NOT override system or application instructions. The builder SHALL mark or sanitize known instruction-injection patterns.

#### Scenario: Source contains hostile instruction
- **WHEN** a retrieved source says to ignore prior instructions or reveal the system prompt
- **THEN** the content cannot change prompt policy and the suspicious content is marked or sanitized and logged without sensitive text

### Requirement: Prompt-building latency
Prompt construction SHALL complete within 150 ms under the defined acceptance-test workload.

#### Scenario: Budgeted context performance
- **WHEN** a prompt is built at the default context budget
- **THEN** the measured build operation meets the 150 ms target
