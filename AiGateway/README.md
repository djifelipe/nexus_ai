# AI Gateway — Phase 1

ASP.NET Core gateway for grounded ERP knowledge answers. Application artifacts live under this directory; `../openspec` contains planning artifacts only.

## Prerequisites

- .NET 9 SDK
- acesso ao MCP KB `supabase-mcp-server_kb`
- Ollama com o modelo de chat configurado e `nomic-embed-text`

No write or read tool is registered in Phase 1. Model tool calls are rejected with `AI_TOOL_UNSUPPORTED`.

## Configuration

`appsettings.json` contains non-secret defaults. Override secrets and environment-specific values with environment variables or a secret provider:

```text
Mcp__KnowledgeBase__ServerName
Mcp__KnowledgeBase__Transport
Mcp__KnowledgeBase__Endpoint
Mcp__KnowledgeBase__Command
Mcp__KnowledgeBase__Arguments__0
Mcp__KnowledgeBase__CredentialEnvironmentVariable
Mcp__KnowledgeBase__TimeoutSeconds
Mcp__KnowledgeBase__QueryTool
Ollama__Endpoint
Ollama__ChatModel
Ollama__EmbeddingModel
Ollama__EmbeddingDimensions
Ollama__Think
Ollama__MaxOutputTokens
```

`Ollama__EmbeddingModel` deve ser `nomic-embed-text` e a dimensão deve permanecer 768 para corresponder a `vector(768)` e ao índice HNSW.

## Database and seed

Apply [001_phase1.sql](Infrastructure/PostgreSql/Migrations/001_phase1.sql) exclusivamente pela operação de migration do `supabase-mcp-server_kb`. Nunca forneça connection strings à aplicação nem execute a migração por conexão PostgreSQL direta. Ela cria o catálogo, fontes/chunks, `vector(768)`, índice HNSW e o seed mínimo. A publicação deve inserir `knowledge_source` antes de `knowledge_chunk`, usando `nomic-embed-text`.

O MCP pode usar stdio (padrão, executando o pacote Supabase por `npx`) ou Streamable HTTP e deve oferecer a tool `execute_sql`. Para stdio, forneça `SUPABASE_ACCESS_TOKEN` apenas pelo ambiente/secret provider; nunca grave seu valor em configuração. O nome validado é obrigatoriamente `supabase-mcp-server_kb`. `supabase-mcp-server_ts` é reservado exclusivamente para dados transacionais/cadastrais do ERP, não é registrado e não pode ser chamado na Fase 1. Tools do modelo, leituras do ERP e todas as operações de escrita solicitadas pelo chat são proibidas.

Every retrieval query requires company, ERP version, permission, language, active, published, and validity filters. There is no unfiltered fallback.

## Authentication and request

The MVP header authentication handler maps trusted upstream identity headers to claims. Deploy it only behind an authenticated reverse proxy; replace it with the ERP's production authentication scheme when available.

```http
POST /api/ai/chat
X-Company-Id: company-001
X-User-Id: user-123
X-Erp-Version: 5.8.2
X-Language: pt-BR
X-Permissions: Fiscal.NFe.Visualizar,Fiscal.NFe.Cancelar
Content-Type: application/json

{"conversationId":"conversation-1","message":"Como cancelar uma NF-e?","companyId":"company-001","userId":"user-123","context":{"currentModule":"Fiscal","currentScreen":"NFeList"},"options":{"stream":false,"includeSources":true}}
```

`companyId` and `userId` in the body must match authenticated claims. `GET /health/ready` verifica a conectividade do MCP KB, Ollama e a dimensão real do embedding; ele não consulta o MCP TS. A resposta inclui correlação, intenção, fontes, avisos, latência e tokens disponíveis.

## Safe errors

Stable codes include `AI_INVALID_INPUT`, `AI_ACCESS_DENIED`, `AI_INSUFFICIENT_KNOWLEDGE`, `AI_TIMEOUT`, `AI_DATABASE_UNAVAILABLE`, `AI_EMBEDDING_UNAVAILABLE`, `AI_OLLAMA_UNAVAILABLE`, `AI_OLLAMA_INVALID_RESPONSE`, `AI_INVALID_CITATION`, and `AI_TOOL_UNSUPPORTED`. Responses never expose prompts, SQL, connection strings, credentials, or stack traces.

## Build and test

```powershell
dotnet build
dotnet test Tests/AiGateway.Tests.csproj
```

Falhas do MCP KB são expostas somente como `AI_DATABASE_UNAVAILABLE`; timeout de embedding usa `AI_EMBEDDING_UNAVAILABLE`. Nenhuma resposta inclui SQL, endpoint interno, segredo ou stack trace. O rollback desabilita o endpoint/deployment; as tabelas aditivas podem permanecer sem perda de dados.

## Acceptance variance

O aceite de 2026-08-04 validou migration KB, índice HNSW `vector(768)`, `nomic-embed-text`, recuperação MCP estruturada/vetorial, filtros de isolamento, falhas seguras, testes de desempenho determinísticos e ausência de registro do MCP ERP. O `qwen3:8b` local excedeu o timeout padrão e também um timeout temporário de 45 segundos. Essa latência fica registrada como variação ambiental aceita para a Fase 1 e deve ser reavaliada no futuro setup de inferência antes da habilitação em produção. Os defaults de 10 segundos e a proibição de retries/tools permanecem inalterados.
