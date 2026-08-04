# Knowledge Graph Expansion Specification

## Purpose
Define bounded, authorized, observable graph expansion for advanced knowledge retrieval.

## Requirements

### Requirement: Bounded authorized graph expansion
The retriever SHALL expand knowledge relations only from authorized seed nodes, with default depth 2, a hard maximum depth 4, configured node/path limits, cycle detection, and an allowlist of relation types. The LLM MUST NOT provide an unrestricted graph query or override these limits.

#### Scenario: Relevant relations enrich retrieval
- **WHEN** authorized seeds have related workflows, permissions, rules, events, entities, or exceptions within the configured limits
- **THEN** the result includes authorized graph paths and graph scores with node, edge, depth, and source provenance

#### Scenario: Requested depth exceeds the maximum
- **WHEN** configuration or input requests graph depth greater than 4
- **THEN** the retriever rejects the value or clamps it to the approved policy without traversing beyond depth 4

#### Scenario: Graph contains a cycle
- **WHEN** traversal reaches a previously visited node and relation path
- **THEN** the cycle is not expanded again and node/path limits remain enforced

### Requirement: Access filters apply at every graph hop
Graph traversal MUST enforce authenticated tenant, ERP version, effective permissions, language, active/publication status, validity dates, and content type before a node or edge becomes visible to ranking, diagnostics, cache, prompt construction, or the caller.

#### Scenario: Related node belongs to another tenant
- **WHEN** an edge points to a semantically relevant node owned by another tenant
- **THEN** the node and its existence details are excluded from paths, scores, diagnostics, and cache

#### Scenario: User lacks permission for an intermediate node
- **WHEN** a path requires an intermediate node outside the user's effective permissions
- **THEN** that path is rejected and traversal does not use it to discover downstream content

#### Scenario: Related content is incompatible or unpublished
- **WHEN** a related node is inactive, unpublished, expired, not yet valid, or incompatible with the company's ERP version
- **THEN** it is excluded before scoring and cannot influence returned candidates

### Requirement: Graph dependency failures are controlled
Graph expansion SHALL obey the shared retrieval deadline and SHALL return explicit degraded diagnostics when graph access times out or fails after structured or vector retrieval succeeds. It MUST fail closed when mandatory access filtering cannot be proven.

#### Scenario: Graph service times out
- **WHEN** graph expansion exceeds its deadline while authorized structured or vector candidates are available
- **THEN** retrieval continues without graph evidence and records a sanitized degraded diagnostic

#### Scenario: Graph filter integrity fails
- **WHEN** the graph adapter cannot prove mandatory tenant, version, permission, and publication filtering
- **THEN** graph results are discarded and no unfiltered path or candidate is returned

### Requirement: Graph expansion is observable without leaking content
The system SHALL record graph duration, seed count, visited node count, retained path count, maximum reached depth, and sanitized failure category, while excluding raw questions, source content, credentials, and cross-tenant identifiers.

#### Scenario: Graph expansion completes
- **WHEN** an expansion attempt completes successfully or in degraded mode
- **THEN** its trace contains bounded low-cardinality diagnostics correlated by request and trace identifiers
