## 1. Domain contracts and configuration

- [x] 1.1 Add `RetrievalAccessScope`, cache-scope fingerprints, graph seed/path, score contribution, deduplication group, and advanced diagnostics records under `AiGateway/Domain/Knowledge`.
- [x] 1.2 Add deterministic retrieval, ranking, relation-allowlist, cache-admission, and access policy contracts under `AiGateway/Domain/Policies`.
- [x] 1.3 Extend `KnowledgeItem` and `RetrievalResult` compatibly with raw/normalized/final scores, graph paths, suppressed-source provenance, policy versions, and degraded outcomes.
- [x] 1.4 Add validated options for intent weights, graph depth/node/path limits, semantic threshold, result/token budgets, sub-timeouts, cache TTLs, feature flags, and schema/policy versions.
- [x] 1.5 Add startup validation that rejects graph depth above 4, invalid relation sets, non-unit weight totals, unsafe TTLs, and missing keyed-digest configuration.

## 2. Authenticated scope and KB MCP contracts

- [x] 2.1 Implement the application service that derives immutable tenant, ERP version, effective permissions, language, validity time, and request identity only from authenticated context.
- [x] 2.2 Extend the `supabase-mcp-server_kb` request/response contracts for filtered structured/vector candidates, authorized graph seeds/traversal, knowledge revision, and source provenance without adding a direct database connection.
- [x] 2.3 Add contract tests proving every structured, vector, and graph call sends tenant, version, permissions, language, validity, publication, active-status, and content-type filters.
- [x] 2.4 Add fail-closed handling and tests for absent/ambiguous permission context, invalid filter acknowledgements, cross-tenant candidates, and incompatible/unpublished content.

## 3. Graph expansion

- [x] 3.1 Define `IGraphKnowledgeExpander` in `AiGateway/Application/Retrieval` and implement its KB MCP-backed adapter under `AiGateway/Infrastructure/Graph`.
- [x] 3.2 Implement authorized seed selection, relation allowlisting, default depth 2/hard depth 4, visited-node cycle control, node/path limits, deadline propagation, and path provenance.
- [x] 3.3 Implement degraded fallback for graph timeout/unavailability and fail-closed behavior when hop-level access filtering cannot be verified.
- [x] 3.4 Add unit and integration tests for relevant expansion, depth clamping/rejection, cycles, limits, permission-filtered intermediate nodes, tenant isolation, version/publication filtering, cancellation, and dependency failure.

## 4. Score fusion and semantic deduplication

- [x] 4.1 Implement per-channel score normalization and a versioned intent-weight policy for `HowTo`, `Explanation`, `PermissionCheck`, `ImpactAnalysis`, and configured defaults.
- [x] 4.2 Implement explainable fusion that preserves raw scores, normalized scores, applied weights, policy version, supporting paths, and zero contribution for unavailable channels.
- [x] 4.3 Implement staged exact/chunk, compatible-version, and semantic deduplication with deterministic tie-breaking and retained suppression provenance.
- [x] 4.4 Protect critical rules and distinct applicability scopes from semantic-only merging and preserve directly matched workflows during result/token budgeting.
- [x] 4.5 Add unit tests for every default intent weight, invalid weights, missing channels, score ordering, obsolete-version suppression, semantic thresholds, critical-rule preservation, and stable tie-breaking.

## 5. Secure search and response cache

- [x] 5.1 Implement canonical access-scope serialization and HMAC/keyed-digest cache identities that exclude raw questions, permission lists, user data, source content, and credentials.
- [x] 5.2 Define cache ports in `AiGateway/Application/Retrieval` and implement Redis-backed search/response cache adapters under `AiGateway/Infrastructure/Redis` with schema/revision metadata and configurable TTLs.
- [x] 5.3 Implement scope/revision validation, publication and policy invalidation hooks, corrupt-entry rejection, local single-flight, and fail-open behavior on Redis errors.
- [x] 5.4 Implement response-cache admission for grounded, non-sensitive, tool-free, fully authorized responses and rejection for unsafe, partial, insufficient, sensitive, user-specific, or tool-derived responses.
- [x] 5.5 Add integration tests for hit/miss, tenant/version/permission separation, revision invalidation, permission revocation, corrupt entries, expiry, Redis outage, concurrent stampede control, and response admission.

## 6. Advanced retrieval orchestration

- [x] 6.1 Update the retriever pipeline in `AiGateway/Application/Retrieval` to perform scoped cache lookup, parallel structured/vector calls, bounded graph expansion, fusion, deduplication, budgeting, and safe cache population in the documented order.
- [x] 6.2 Propagate one cancellation/deadline budget across adapters and enforce configurable sub-budgets while targeting the 800 ms acceptance workload.
- [x] 6.3 Distinguish authorized empty results, degraded optional dependencies, KB MCP dependency failures, authorization/filter-integrity failures, and cancellation without unfiltered fallback.
- [x] 6.4 Update prompt/orchestration integration to consume only retained authorized sources and ensure citations cannot reference suppressed or filtered items.
- [x] 6.5 Add compatibility tests proving the required `POST /api/ai/chat` HTTP contract remains unchanged while advanced diagnostics stay internal or in approved metrics.

## 7. Observability, privacy, and security verification

- [x] 7.1 Add spans for cache, structured, vector, graph, fusion, and deduplication with durations, bounded counts, policy versions, cache outcomes, graph limits, and degraded categories.
- [x] 7.2 Add low-cardinality metrics for cache effectiveness, graph/fusion/deduplication latency, filtered candidates, invalidations, dependency degradation, and budget enforcement.
- [x] 7.3 Verify logs, traces, metrics, diagnostics, and cache keys omit raw questions, source content, credentials, permission lists, personal data, and cross-tenant identifiers.
- [x] 7.4 Add security tests for cache side channels, cross-tenant graph paths, permission/version changes, prompt-visible provenance, malicious MCP payloads, and telemetry failure tolerance.

## 8. Rollout and acceptance

- [x] 8.1 Add independent feature flags for advanced ranking, graph expansion, search cache, and response cache, with Phase 1 behavior as the rollback path.
- [x] 8.2 Add shadow-mode comparison and a curated relevance corpus covering `HowTo`, `Explanation`, `PermissionCheck`, `ImpactAnalysis`, ambiguity, insufficient knowledge, access denied, and external failure.
- [x] 8.3 Document deployment order, configuration, KB MCP graph prerequisite, Redis operation, revision/invalidation contract, dashboards, alerts, and rollback under `AiGateway`.
- [x] 8.4 Run formatting, static analysis, architecture checks, unit tests, integration tests, tenant-isolation/security suites, and the complete solution build; resolve all failures.
- [x] 8.5 Benchmark the acceptance workload and verify retrieval targets 800 ms, graph depth never exceeds 4, budgets remain at 15 items/8000 tokens by default, and all specification scenarios pass.
