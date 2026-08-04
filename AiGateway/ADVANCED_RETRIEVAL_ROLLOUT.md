# Advanced retrieval rollout

Phase 2 is controlled by independent `AdvancedRetrieval` flags: `AdvancedRankingEnabled`, `GraphEnabled`, `SearchCacheEnabled`, and `ResponseCacheEnabled`. Disable them in reverse order to restore Phase 1 ranking without a data migration.

## Deployment order

1. Deploy domain contracts, diagnostics, and advanced ranking in shadow mode.
2. Confirm `supabase-mcp-server_kb` exposes the filtered `knowledge_relation` traversal and tenant knowledge revision contract. The application must never connect directly to the knowledge database or graph store.
3. Compare the shadow ranking against `Tests/TestData/advanced-retrieval-relevance.json` and production quality metrics.
4. Configure Redis through `RetrievalCache:RedisConfiguration`; keep cache flags disabled while health, latency, and key isolation are verified.
5. Set a secret `AdvancedRetrieval:CacheKeySecret` through the deployment secret provider. Never store the production secret in appsettings or logs.
6. Enable search cache, then graph expansion, then response cache for a small tenant cohort. Response caching is restricted to grounded, non-sensitive, tool-free responses.

## Revision and invalidation

Cache entries carry tenant knowledge revision, ERP version, permission fingerprint, schema version, and ranking policy version. A mismatch is a cache miss. Publication/withdrawal should advance the tenant revision; short TTLs are the fallback until publisher events trigger targeted removal.

## Dashboards and alerts

Track `ai.retrieval.cache`, `sql`, `pgvector`, `graph`, `fusion`, and `deduplication` duration and outcomes. Alert on retrieval p95 above 800 ms, graph degradation, cache error spikes, filter-integrity failures, cross-tenant security tests, empty-result regression, and response-cache admission anomalies. Telemetry must not contain raw questions, source content, cache keys, permissions, or credentials.

## Rollback

Disable `ResponseCacheEnabled`, `SearchCacheEnabled`, `GraphEnabled`, and `AdvancedRankingEnabled`. Cached entries may expire naturally or be deleted by the scoped operational invalidation procedure. No business or knowledge data is written by this phase.
