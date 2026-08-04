## 1. Foundation and project structure

- [x] 1.1 Treat `C:\github.com\djifelipe\nexus_ai\AiGateway` as the mandatory application root and create the topic-11 directory and namespace structure beneath it under `Api`, `Application`, `Domain`, `Infrastructure`, and `Workers`, including reserved Phase 1/future component folders
- [x] 1.2 Add required .NET dependencies for MCP client transport, options validation, authentication, HTTP resilience, and observability with compatible pinned versions; remove direct PostgreSQL/pgvector client dependencies
- [x] 1.3 Define validated configuration for `supabase-mcp-server_kb`, embeddings, Ollama model/endpoint, token budgets, timeouts, confidence thresholds, and feature enablement without storing database secrets or connection strings
- [x] 1.4 Add an architecture test that prevents `Domain` and `Application` from depending on `Api` or concrete `Infrastructure` adapters

## 2. Domain and application contracts

- [x] 2.1 Implement domain records/enums for authenticated user context, intent, knowledge items, prompt packages, model responses, validation results, sources, and final AI responses
- [x] 2.2 Define `IAiOrchestrator`, `IIntentRouter`, `IKnowledgeRetriever`, `IPromptBuilder`, `ILanguageModelClient`, `IResponseValidator`, `IAiTelemetry`, `IKnowledgeBaseMcpClient`, reserved `IErpMcpClient`, embedding, token-estimator, and sanitizer ports with cancellation support
- [x] 2.3 Define API request/response contracts and validation rules for `POST /api/ai/chat`, including conversation, screen context, source options, warnings, and metrics
- [x] 2.4 Implement stable application error codes and mappings for invalid input, access denied, insufficient knowledge, cancellation, timeout, database/embedding/Ollama failure, invalid citation, and unsupported tool requests

## 3. Authentication, identity, and request safety

- [x] 3.1 Configure authentication/authorization and build immutable `UserContext` from claims for tenant, user, ERP version, language, permissions, and current application context
- [x] 3.2 Implement middleware for request/trace/conversation correlation, cancellation propagation, safe exception mapping, and total request timing
- [x] 3.3 Reject payload company/user values that conflict with authenticated identity before any intent or retrieval operation
- [x] 3.4 Implement and unit-test allowlisted structured logging plus sensitive-data sanitization for credentials, tokens, connection strings, prompts, stack traces, and unnecessary personal/fiscal/banking data

## 4. Rule-based intent routing

- [x] 4.1 Add migration and seed support for modules, features, actions, entities, intent terms/aliases, weights, relationships, and active status, applied exclusively through `supabase-mcp-server_kb`
- [x] 4.2 Implement normalization and deterministic scoring from question terms, aliases, catalog relationships, and authorized screen context while retaining the determining strategy
- [x] 4.3 Implement thresholds for `Unknown`, multi-module candidates, and clarification, ensuring only existing and authorized catalog identifiers/choices are emitted
- [x] 4.4 Add unit tests for exact matches, aliases, invalid identifiers, screen-context disambiguation, ambiguous cancellation, confidence boundaries, and the known corpus accuracy target
- [x] 4.5 Add a performance test that verifies rule-only routing against the 300 ms acceptance target

## 5. MCP KB and pgvector retrieval

- [x] 5.1 Add pgvector-enabled migrations for knowledge sources/chunks and scope metadata, applying them exclusively through `supabase-mcp-server_kb`
- [x] 5.2 Implement structured knowledge retrieval through `supabase-mcp-server_kb`, prioritizing exact workflows/rules/features and requiring tenant, version, permission, active, publication, and validity filters
- [x] 5.3 Implement the embedding port and pgvector similarity retrieval through `supabase-mcp-server_kb` with the identical mandatory filters and model-dimension validation
- [x] 5.4 Implement deterministic Phase 1 result interleaving, traceable diagnostics, configurable maximum of 15 results, and 8000-token context budget without splitting critical rules
- [x] 5.5 Add `supabase-mcp-server_kb`/pgvector integration tests for covered questions, ordering, empty results, version mismatch, unpublished/expired content, permission denial, cross-tenant isolation, and proof that `supabase-mcp-server_ts` is not invoked
- [x] 5.6 Add failure/timeout and performance tests verifying controlled dependency errors, no unfiltered fallback, and the 800 ms retrieval target

## 6. Grounded prompt building

- [x] 6.1 Implement structured messages for system policy, authenticated context, intent, identified knowledge, optional conversation summary, and unchanged original question
- [x] 6.2 Implement source prioritization and token estimation with configurable reserve, preserving critical rules and dropping lower-priority sources first
- [x] 6.3 Delimit retrieved sources as untrusted data and implement marking/sanitization of known prompt-injection patterns
- [x] 6.4 Add unit tests for source IDs, access-validation preconditions, priority order, token overflow, intact critical rules/question, and hostile source instructions
- [x] 6.5 Add a performance test for the 150 ms prompt-building target at the default context budget

## 7. Ollama integration

- [x] 7.1 Implement the Ollama `HttpClient` adapter with typed endpoint/model options, strict request/response mapping, cancellation, and a timeout compatible with the total SLA
- [x] 7.2 Map returned content, finish data, and available prompt/completion token counts without fabricating unavailable usage
- [x] 7.3 Reject Phase 1 tool calls and map network, timeout, cancellation, and malformed-response failures to sanitized stable errors without unsafe retries
- [x] 7.4 Add integration tests using a simulated Ollama server for success, token usage, timeout, cancellation, malformed response, tool request, and first-token measurement when streaming is enabled

## 8. Citation validation and orchestration

- [x] 8.1 Implement deterministic `[source-id]` extraction and membership validation against the exact sources in the prompt package
- [x] 8.2 Implement safe `InsufficientKnowledge` behavior for empty retrieval and factual answers without required valid citations, and reject invented citations as non-grounded
- [x] 8.3 Implement `AiOrchestrator` sequencing with cancellation, correlation, per-stage timing, no tool-execution loop, and final confidence/status/source mapping
- [x] 8.4 Implement the protected chat endpoint and dependency registration across Application and Infrastructure
- [x] 8.5 Add unit tests for valid, missing, duplicate, malformed, and invented citations plus the 300 ms validation target
- [x] 8.6 Add API tests for grounded success, ambiguous intent, unknown intent, insufficient knowledge, authentication failure, identity conflict, unsupported tool request, and external failures

## 9. Telemetry and operational readiness

- [x] 9.1 Implement non-blocking telemetry scopes and spans for request, intent, SQL/vector retrieval, prompt, Ollama, validation, and response
- [x] 9.2 Record controlled-cardinality latency, volume, error, token, grounded/insufficient-knowledge, and source-use metrics without placing tenant/user IDs in metric labels
- [x] 9.3 Add tests proving telemetry sink failure does not change a successful response and sensitive values never reach logs or metrics
- [x] 9.4 Add health/readiness checks for MCP KB configuration/connectivity, embedding compatibility, and Ollama with safe diagnostic output; do not probe MCP TS in Phase 1
- [x] 9.5 Document MCP KB configuration, migrations, knowledge seed/index workflow, MCP server segregation, endpoint examples, expected errors, and the explicit prohibition of ERP/tools access in Phase 1

## 10. Verification and acceptance

- [x] 10.1 Apply migrations through a clean `supabase-mcp-server_kb` environment and execute the full unit, architecture, MCP integration, API, isolation, security, and observability test suites
- [x] 10.2 Run `dotnet build` with warnings reviewed and `dotnet test` with all tests passing
- [x] 10.3 Exercise an end-to-end grounded question through `supabase-mcp-server_kb` and Ollama, verifying response contract, citations, tenant isolation, tokens, telemetry, and zero calls to `supabase-mcp-server_ts`
- [x] 10.4 Execute SLA acceptance tests for intent, retrieval, prompt, first token when supported, validation, and complete no-tool response, documenting any environment-dependent variance
- [x] 10.5 Verify every Phase 1 spec scenario, confirm every application artifact is under `C:\github.com\djifelipe\nexus_ai\AiGateway`, confirm no read/write tools, graph, cache, or advanced validation were accidentally introduced, and record deployment/rollback readiness
