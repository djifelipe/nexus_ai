## Context

Phase 1 retrieves authorized knowledge through structured and vector searches delegated to `supabase-mcp-server_kb`. Phase 2 must improve relevance and latency by adding bounded graph expansion, intent-sensitive score fusion, semantic deduplication, stronger version/permission enforcement, and caching. The application remains rooted at `AiGateway` and follows the `Api`, `Application`, `Domain`, `Infrastructure`, and `Workers` separation from the target architecture.

The authenticated context is the authority for tenant, company ERP version, user permissions, language, and request identity. Neither LLM output nor client-supplied cache identifiers may grant access. Knowledge access remains exclusive to `supabase-mcp-server_kb`; Phase 2 does not enable `supabase-mcp-server_ts` or direct database connections.

## Goals / Non-Goals

**Goals:**

- Combine structured, vector, and graph evidence into an ordered, explainable result within the retrieval budget.
- Expand only authorized graph relations with default depth 2, hard maximum depth 4, cycle control, path limits, and deterministic timeouts.
- Remove redundant chunks, obsolete versions, and semantic equivalents while retaining provenance.
- Apply tenant, ERP version, permissions, language, validity, publication, and content-type filters before candidates become observable, rankable, or cacheable.
- Cache searches and eligible final responses without allowing reuse across different access contexts.
- Preserve the 800 ms retrieval target and expose enough diagnostics to tune quality and latency.
- Keep implementation responsibilities aligned with the project structure described in topic 11.

**Non-Goals:**

- Read-only or write tools over transactional ERP data.
- Advanced claim extraction, semantic response validation, or automatic regeneration.
- Graph-driven authorization decisions without deterministic permission checks.
- Direct PostgreSQL, pgvector, Supabase, Redis-backed source-of-truth, or ERP database access outside approved adapters.
- Changes to the mandatory HTTP shape of `POST /api/ai/chat`.

## Decisions

### 1. Layer responsibilities follow the proposed project structure

- `Domain/Knowledge` owns candidates, graph paths, score components, deduplication groups, cache scope values, and retrieval diagnostics.
- `Domain/Policies` owns deterministic access-scope and ranking policy contracts.
- `Application/Retrieval` coordinates cache lookup, parallel structured/vector retrieval, graph expansion, authorization validation, fusion, deduplication, token budgeting, and cache population.
- `Infrastructure/Graph` implements the graph port; any graph content derived from the knowledge base must be obtained through capabilities exposed by `supabase-mcp-server_kb`, not a direct database connection.
- `Infrastructure/Redis` implements distributed cache storage and invalidation. Redis is an optimization, never an authorization or source-of-truth system.
- `Infrastructure/Observability` records spans and low-cardinality metrics without raw questions or source content.
- `Workers/KnowledgePublisher` may publish revision/invalidation events after the underlying knowledge publication flow is available; request-time revision checks remain the correctness fallback.

This keeps policy independent of infrastructure. An alternative was to place ranking and cache logic in the MCP adapter, but that would couple domain behavior to transport and make deterministic testing harder.

### 2. Retrieval pipeline is ordered around access safety

The application derives an immutable `RetrievalAccessScope` from authenticated context. A request then follows:

1. Normalize the question for retrieval and compute a keyed hash for cache identity.
2. Build the cache scope from tenant, ERP version, sorted effective permissions, language, intent, retrieval policy version, and knowledge revision.
3. Attempt an L1/L2 search-cache read; validate stored scope and revision before use.
4. On miss, invoke structured and vector searches concurrently through `supabase-mcp-server_kb`, passing all mandatory filters.
5. Select authorized seed nodes and perform bounded graph expansion.
6. Normalize scores, fuse candidates by intent policy, deduplicate, enforce result/token budgets, and record diagnostics.
7. Cache only the authorized final retrieval result. The response cache is populated only for grounded, non-sensitive, tool-free responses and uses the same access scope plus prompt/model policy versions.

Filtering after ranking was rejected because unauthorized candidates could influence scores or leak through diagnostics and cache behavior.

### 3. Graph expansion is bounded and fail-soft

`IGraphKnowledgeExpander` accepts authorized seeds, relation allowlists, access scope, depth, maximum paths/nodes, and a deadline. Default depth is 2 and configuration above 4 is rejected. Traversal tracks visited node/relation pairs, rejects cycles, and returns source/path provenance. Permission and publication filters apply at every hop.

Graph timeout or dependency failure records a degraded diagnostic and continues with structured/vector evidence when those channels succeeded. Access-filter failures fail closed. Unrestricted traversal or an LLM-selected raw graph query is not permitted.

### 4. Fusion is policy-driven and explainable

Each channel score is normalized to `[0,1]`. The default weights are:

| Intent | SQL | Vector | Graph |
| --- | ---: | ---: | ---: |
| HowTo | 0.45 | 0.35 | 0.20 |
| Explanation | 0.20 | 0.50 | 0.30 |
| PermissionCheck | 0.60 | 0.05 | 0.35 |
| ImpactAnalysis | 0.25 | 0.20 | 0.55 |

Other intents use a configured default whose weights sum to 1. Missing channels contribute zero and available weights are not silently inflated. Each result retains raw scores, normalized scores, policy version, applied weights, and supporting graph paths.

A learned reranker was deferred because Phase 2 needs deterministic, auditable behavior and no labeled training set is defined.

### 5. Deduplication is staged and provenance-preserving

Deduplication first removes exact source/chunk duplicates, then keeps the newest compatible published version, then clusters semantic equivalents above a configurable similarity threshold. Within a cluster the highest fused score wins, with critical rules and exact workflows favored on ties. The retained item records suppressed source IDs and reasons so citations and audit remain traceable. Critical rules are never merged solely because of semantic similarity.

### 6. Cache identity is access-scoped, opaque, and revisioned

Cache keys use an HMAC or keyed digest over canonical scope fields; they never embed raw questions, permissions, user identifiers, or source content. Permission sets are sorted and hashed. Entries include schema version, retrieval/ranking policy versions, tenant knowledge revision, ERP version, effective permission fingerprint, creation time, and expiry.

Search cache TTL defaults to 5 minutes and eligible response cache TTL to 2 minutes, both configurable. Revision mismatch, permission/version change, publication events, or schema/policy changes cause a miss or invalidation. Cache failures are fail-open for availability but never bypass live authorization checks or return unverifiable data. Local single-flight prevents a cache stampede for identical scoped keys.

Caching only by normalized question was rejected because it could leak tenant- or permission-specific content.

### 7. Time budgets and failure semantics are explicit

The orchestrator propagates one retrieval deadline. Suggested initial sub-budgets are 450 ms for structured/vector retrieval, 200 ms for graph expansion, and 100 ms for fusion/deduplication/cache overhead, leaving margin within the 800 ms target. Cancellation is propagated to every adapter.

No authorized candidates is a successful empty result. A graph or cache outage may produce a degraded successful result. Loss of the KB MCP service, inability to establish access scope, or any filter-integrity failure returns a controlled dependency/security error with no unfiltered fallback.

### 8. Observability avoids sensitive payloads

Spans cover `ai.retrieval.cache`, `sql`, `vector`, `graph`, `fusion`, and `deduplication`. Diagnostics include strategy durations, candidate/result counts, applied filter names, weight policy, cache outcome, graph depth/path counts, and deduplication counts. Metrics use tenant-safe aggregation and do not include raw question, permission names, source text, cache keys, or sensitive identifiers. Telemetry failure does not fail retrieval.

### 9. Verification combines unit, contract, integration, and security tests

Unit tests cover weight policies, depth limits, cycles, version precedence, semantic thresholds, cache canonicalization, and budget enforcement. MCP contract tests prove mandatory filters are sent. Redis/graph integration tests cover invalidation, outage, corrupt entries, and timeouts. Security tests prove cross-tenant, permission, version, and publication isolation, including cache side channels. End-to-end tests verify citations reference only retained sources and benchmark the 800 ms target.

## Risks / Trade-offs

- [Graph expansion increases latency and noise] → Enforce allowlisted relations, depth/path/node limits, intent-specific expansion, deadlines, and degraded fallback.
- [Semantic deduplication removes distinct rules] → Exclude critical rules from semantic-only merging, retain provenance, and tune thresholds with curated tests.
- [Stale cache returns revoked or obsolete knowledge] → Scope by permission/version/revision, use short TTLs, validate entry metadata, and support event-driven invalidation.
- [Cache keys leak user questions or access details] → Use keyed digests and keep raw values out of keys, logs, and metrics.
- [Different channel score distributions distort fusion] → Normalize per strategy, version policies, retain diagnostics, and test with a fixed relevance corpus.
- [Fail-soft behavior hides dependency degradation] → Return explicit degraded diagnostics and alerts while preserving safe available evidence.
- [MCP graph capability is unavailable] → Implement the port and feature flag first; keep graph disabled until the KB MCP contract supports filtered traversal.

## Migration Plan

1. Add domain/application contracts and diagnostics with graph and cache feature flags disabled.
2. Extend the KB MCP contract and tests for filtered seed lookup and bounded traversal; deploy without enabling traffic.
3. Add fusion and deduplication in shadow mode, comparing rankings with Phase 1 while returning the existing result.
4. Enable advanced ranking for a limited tenant cohort and monitor relevance, isolation, latency, and empty-result rates.
5. Deploy Redis cache adapters with reads/writes disabled, then enable search cache and finally eligible response cache.
6. Enable graph expansion by intent/cohort after latency and relevance acceptance gates pass.
7. Roll back by disabling response cache, search cache, graph, and advanced ranking independently; no persisted business data requires rollback.

## Open Questions

- Which graph engine or KB MCP graph capability will be standardized for the first production deployment?
- What publication mechanism will expose a monotonic tenant knowledge revision for invalidation?
- Which curated relevance corpus and threshold define an acceptable improvement over Phase 1?
- Should response caching initially be limited to `HowTo` and `Explanation` intents?
