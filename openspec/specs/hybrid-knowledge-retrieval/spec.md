# Hybrid Knowledge Retrieval Specification

## Purpose
Define secure, tenant-filtered retrieval through the designated MCP servers with bounded context and controlled failures.

## Requirements

### Requirement: Knowledge access through the KB MCP server
The retriever SHALL query structured knowledge and pgvector semantic chunks exclusively through `supabase-mcp-server_kb` and return ordered, traceable knowledge items with source IDs and retrieval diagnostics. The application MUST NOT open a direct database connection or hold a knowledge-base connection string.

#### Scenario: Covered question retrieves sources
- **WHEN** an authorized covered question has matching structured and semantic knowledge
- **THEN** the result contains at least one directly related source ordered by the Phase 1 ranking and records query strategy and scores

#### Scenario: Direct database access is attempted
- **WHEN** an application component attempts to open PostgreSQL/Supabase directly for knowledge data
- **THEN** architecture validation fails and the component must use `supabase-mcp-server_kb`

### Requirement: ERP database segregation
Any access to transactional or master ERP data MUST use `supabase-mcp-server_ts` exclusively. Phase 1 SHALL NOT register or invoke that server because ERP data tools are outside the MVP.

#### Scenario: Phase 1 processes a knowledge question
- **WHEN** the Phase 1 pipeline retrieves sources and generates an answer
- **THEN** it invokes only `supabase-mcp-server_kb` and never invokes `supabase-mcp-server_ts`

### Requirement: Mandatory access and publication filters
Every structured and vector request to `supabase-mcp-server_kb` MUST include effective tenant, ERP version, permissions, active status, publication status, and validity dates, and the server MUST apply them before content is returned to the application or model.

#### Scenario: Cross-tenant candidate exists
- **WHEN** a semantically closer chunk belongs to another tenant
- **THEN** that chunk is excluded by the database query and never reaches prompt construction

#### Scenario: User lacks source permission
- **WHEN** matching knowledge requires a permission absent from the authenticated context
- **THEN** the source is excluded and the result discloses no title, content, or existence details

#### Scenario: Inactive or incompatible knowledge exists
- **WHEN** a matching source is unpublished, expired, inactive, or incompatible with the company's ERP version
- **THEN** the source is excluded from both SQL and vector results

### Requirement: Retrieval budgets
The retriever SHALL enforce configurable result and context budgets, defaulting to 15 items and 8000 estimated context tokens, and SHALL preserve the highest-priority authorized sources.

#### Scenario: Candidates exceed budget
- **WHEN** authorized candidates exceed either configured budget
- **THEN** lower-priority candidates are omitted and diagnostics report the applied limits without truncating a critical rule mid-content

### Requirement: Retrieval failure behavior
The retriever MUST distinguish no authorized knowledge from dependency failure and SHALL target completion within 800 ms under the acceptance-test workload.

#### Scenario: No authorized knowledge
- **WHEN** all matching sources are filtered out or no source matches
- **THEN** the retriever returns an empty successful result that the pipeline can map to insufficient knowledge

#### Scenario: KB MCP server unavailable
- **WHEN** `supabase-mcp-server_kb` is unavailable or the retrieval timeout expires
- **THEN** the retriever returns a controlled dependency error and no unfiltered fallback data
