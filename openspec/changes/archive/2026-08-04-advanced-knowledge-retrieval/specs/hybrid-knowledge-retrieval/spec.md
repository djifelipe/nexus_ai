## MODIFIED Requirements

### Requirement: Knowledge access through the KB MCP server
The retriever SHALL query structured knowledge, pgvector semantic chunks, and knowledge graph capabilities exclusively through `supabase-mcp-server_kb` and return ordered, traceable knowledge items with source IDs, graph paths, per-strategy scores, final scores, and retrieval diagnostics. The application MUST NOT open a direct database or graph-store connection or hold a knowledge-base connection string.

#### Scenario: Covered question retrieves fused sources
- **WHEN** an authorized covered question has matching structured, semantic, or graph knowledge
- **THEN** the result contains at least one directly related source ordered by the Phase 2 fusion policy and records contributing strategies, scores, paths, and filters

#### Scenario: Direct knowledge-store access is attempted
- **WHEN** an application component attempts to open PostgreSQL, Supabase, pgvector, or the knowledge graph directly
- **THEN** architecture validation fails and the component must use `supabase-mcp-server_kb`

### Requirement: Mandatory access and publication filters
Every structured, vector, and graph request to `supabase-mcp-server_kb` MUST include authenticated tenant, company ERP version, effective permissions, language, active status, publication status, validity dates, and content type. These filters MUST apply before candidates are returned, ranked, fused, deduplicated, cached, logged, or sent to the model.

#### Scenario: Cross-tenant candidate exists
- **WHEN** a higher-scoring chunk or graph node belongs to another tenant
- **THEN** it is excluded before ranking and never affects scores, cache behavior, diagnostics, or prompt construction

#### Scenario: User lacks source permission
- **WHEN** matching knowledge requires a permission absent from the authenticated context
- **THEN** the source is excluded and the result discloses no title, content, relation, score, or existence details

#### Scenario: Inactive or incompatible knowledge exists
- **WHEN** a matching source is unpublished, expired, not yet valid, inactive, or incompatible with the company's ERP version
- **THEN** it is excluded from structured, vector, graph, cache, and fused results

#### Scenario: Permission context is ambiguous or unavailable
- **WHEN** effective permissions cannot be derived deterministically from authenticated context
- **THEN** retrieval fails closed with a controlled authorization error and performs no unfiltered fallback

## ADDED Requirements

### Requirement: Intent-sensitive score fusion
The retriever SHALL normalize structured, vector, and graph scores to a common range and calculate an explainable final score using a versioned deterministic weight policy selected by intent. Configured weights MUST be validated and sum to 1 for each policy.

#### Scenario: HowTo intent is fused
- **WHEN** a `HowTo` request has evidence from all strategies
- **THEN** the default final score uses SQL 0.45, vector 0.35, and graph 0.20 and exposes each contribution in diagnostics

#### Scenario: Explanation intent is fused
- **WHEN** an `Explanation` request has evidence from all strategies
- **THEN** the default final score uses SQL 0.20, vector 0.50, and graph 0.30

#### Scenario: PermissionCheck intent is fused
- **WHEN** a `PermissionCheck` request has evidence from all strategies
- **THEN** the default final score uses SQL 0.60, vector 0.05, and graph 0.35

#### Scenario: ImpactAnalysis intent is fused
- **WHEN** an `ImpactAnalysis` request has evidence from all strategies
- **THEN** the default final score uses SQL 0.25, vector 0.20, and graph 0.55

#### Scenario: A strategy is unavailable
- **WHEN** one optional retrieval strategy fails or returns no authorized evidence
- **THEN** its contribution is zero, available evidence remains traceable, and degraded diagnostics identify the absent strategy without inventing a score

### Requirement: Semantic and version-aware deduplication
Before token budgeting, the retriever SHALL remove exact duplicates, redundant chunks, obsolete versions when a newer compatible published version exists, and semantic equivalents above a configured threshold. It MUST retain the winning item's provenance and suppression reasons and MUST NOT merge critical rules solely by semantic similarity.

#### Scenario: Redundant chunks from one document are returned
- **WHEN** multiple chunks contain materially equivalent content from the same source version
- **THEN** the highest-ranked representative is retained and suppressed chunk IDs are recorded for audit

#### Scenario: Current and obsolete versions match
- **WHEN** equivalent sources include a current compatible published version and an older version
- **THEN** the current compatible version is retained regardless of the obsolete version's raw vector score

#### Scenario: Distinct critical rules appear similar
- **WHEN** two authorized critical rules exceed the semantic similarity threshold but have different identifiers or applicability
- **THEN** both remain available unless an explicit deterministic equivalence rule applies

### Requirement: Advanced retrieval remains within budgets
The advanced retriever SHALL preserve the configured default maximum of 15 items and 8000 estimated context tokens and SHALL target completion within 800 ms under the acceptance-test workload, including cache, graph, fusion, and deduplication overhead.

#### Scenario: Advanced candidates exceed result or token budget
- **WHEN** fused authorized candidates exceed either budget
- **THEN** lower-ranked redundant candidates are omitted after deduplication while critical rules and exact workflows are preserved without mid-content truncation

#### Scenario: Shared retrieval deadline expires
- **WHEN** optional graph or cache work would exceed the shared retrieval deadline
- **THEN** it is cancelled and the retriever returns safe available evidence with degraded diagnostics, or a controlled dependency error if no safe evidence exists

### Requirement: Advanced retrieval diagnostics are auditable and sanitized
The retriever SHALL record durations, candidate counts, normalized and final scores, policy version, applied filter names, graph path summaries, deduplication decisions, cache outcome, and budget decisions. Diagnostics MUST NOT include credentials, raw permission sets, cross-tenant identifiers, hidden source content, or sensitive user data.

#### Scenario: Retrieval completes successfully
- **WHEN** advanced retrieval returns items or an authorized empty result
- **THEN** diagnostics allow reconstruction of ranking decisions using sanitized metadata correlated by request and trace identifiers

#### Scenario: No authorized knowledge remains
- **WHEN** filtering and deduplication leave no authorized candidate
- **THEN** the retriever returns a successful empty result that maps to insufficient knowledge without revealing filtered candidates
