## ADDED Requirements

### Requirement: Cache entries are isolated by effective access scope
Search and response cache keys MUST include opaque fingerprints of tenant, company ERP version, sorted effective permissions, language, normalized query, structured intent, policy/schema versions, and tenant knowledge revision. Keys and telemetry MUST NOT contain raw questions, source content, credentials, personal data, or permission lists.

#### Scenario: Equivalent request has the same access scope
- **WHEN** an equivalent request repeats with the same authenticated access scope and current revisions
- **THEN** the system may reuse the authorized cache entry and records a cache hit

#### Scenario: Tenant differs
- **WHEN** two requests have the same question and intent but different tenants
- **THEN** their keys differ and neither request can observe or reuse the other's entry

#### Scenario: Permission or ERP version differs
- **WHEN** a user's effective permissions or the company's ERP version changes
- **THEN** the previous entry is not reused and no inaccessible source metadata is disclosed

### Requirement: Cached data is revisioned and invalidated
Every cache entry SHALL carry expiry, schema version, retrieval/ranking policy version, knowledge revision, ERP version, and permission fingerprint. Publication, withdrawal, permission, version, validity, schema, or policy changes MUST cause affected entries to be invalidated or treated as misses.

#### Scenario: Knowledge is republished
- **WHEN** a tenant's relevant knowledge revision advances after an entry was stored
- **THEN** the stale entry is ignored and retrieval obtains current authorized sources

#### Scenario: Source permission is revoked
- **WHEN** a permission change makes a cached source inaccessible
- **THEN** the entry cannot be served under the new permission fingerprint even before its TTL expires

#### Scenario: Cache metadata is corrupt or incomplete
- **WHEN** an entry lacks required scope, revision, or schema metadata
- **THEN** the system discards it as a miss and does not expose its payload

### Requirement: Cache is an optional fail-open optimization
Redis or local cache failure SHALL NOT bypass live authorization, SHALL NOT become an unfiltered fallback, and SHALL NOT prevent safe retrieval when `supabase-mcp-server_kb` remains available. Concurrent misses for the same scoped key SHALL be coalesced where practical.

#### Scenario: Redis is unavailable
- **WHEN** cache read or write fails
- **THEN** the system retrieves knowledge through the normal authorized pipeline, records a sanitized degraded cache outcome, and returns no stale unverifiable entry

#### Scenario: Concurrent identical misses occur
- **WHEN** multiple requests with the same scoped key miss concurrently
- **THEN** the system limits duplicate backend work without sharing results across access scopes

### Requirement: Response cache admits only safe deterministic responses
The response cache SHALL store only successfully validated, grounded, non-sensitive, tool-free responses whose source set and access scope are cache-eligible. Unsafe, partially grounded, insufficient-knowledge, tool-derived, or user-specific responses MUST NOT be stored as reusable responses.

#### Scenario: Grounded knowledge-only response is produced
- **WHEN** validation succeeds and every cited source belongs to the cache-scoped authorized retrieval result
- **THEN** the response may be cached with its source provenance and short configured expiry

#### Scenario: Response used a tool or contains sensitive data
- **WHEN** generation depends on a tool result or sanitization marks the response as sensitive
- **THEN** the response is not added to the reusable response cache

### Requirement: Cache operations are observable and privacy preserving
The system SHALL measure cache hit, miss, stale, invalid, bypass, error, latency, and coalescing outcomes by cache type without logging cache keys or payloads.

#### Scenario: Cache operation completes
- **WHEN** a search-cache or response-cache operation completes
- **THEN** a trace records cache type, outcome, duration, and sanitized reason correlated to the request
