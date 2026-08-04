## ADDED Requirements

### Requirement: Catalog-validated rule routing
The intent router SHALL classify module, feature, action, entity, type, confidence, keywords, strategy, and candidate modules using deterministic rules, aliases, catalog entries, and authorized screen context. It MUST NOT return identifiers absent from the catalog.

#### Scenario: Exact known intent
- **WHEN** the question contains known terms that uniquely identify a catalog module, feature, and action
- **THEN** the router returns those identifiers, the applicable intent type, confidence, and the determining rule strategy

#### Scenario: Screen context disambiguates intent
- **WHEN** a question is ambiguous but its authenticated screen context uniquely identifies a valid candidate
- **THEN** the context raises that candidate's score without replacing or altering the original question

### Requirement: Confidence and ambiguity handling
The router MUST return `Unknown` below confidence 0.55 and SHALL request clarification when multiple plausible intents cannot be resolved. Results from 0.55 through 0.75 SHALL retain authorized candidate modules for broader retrieval.

#### Scenario: Unknown question
- **WHEN** no valid catalog intent reaches confidence 0.55
- **THEN** the result type is `Unknown` and no invented module, feature, or action is emitted

#### Scenario: Ambiguous cancellation
- **WHEN** the user asks how to cancel without screen context and multiple catalog actions match
- **THEN** the result requires clarification and includes a question naming only authorized valid choices

### Requirement: Rule-routing latency
Rule-only intent routing SHALL complete within 300 ms under the defined acceptance-test workload.

#### Scenario: Known rule performance
- **WHEN** the known-intent acceptance corpus is executed without external LLM calls
- **THEN** each measured routing operation meets the 300 ms target and the corpus classification accuracy is at least 90 percent

