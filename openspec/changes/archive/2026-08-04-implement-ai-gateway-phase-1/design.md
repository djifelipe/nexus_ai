## Context

O projeto atual é uma API ASP.NET Core mínima cuja raiz obrigatória é `C:\github.com\djifelipe\nexus_ai\AiGateway`. A Fase 1 introduz, exclusivamente sob essa raiz, um fluxo vertical completo para perguntas fundamentadas, integração com `supabase-mcp-server_kb`, Ollama e controles transversais de identidade, segurança e telemetria. Os artefatos OpenSpec permanecem em `C:\github.com\djifelipe\nexus_ai\openspec`.

O fluxo será: middleware de correlação e identidade → endpoint de chat → `AiOrchestrator` → roteamento por regras → recuperação SQL/vetorial → construção do prompt → Ollama → validação de citações → resposta e telemetria. O contexto autenticado é a fonte de verdade para tenant, usuário, versão e permissões.

## Goals / Non-Goals

**Goals:**

- Entregar `POST /api/ai/chat` funcional, testável e com contratos estáveis.
- Preservar, sob `C:\github.com\djifelipe\nexus_ai\AiGateway`, as camadas e áreas do tópico 11: `Api`, `Application`, `Domain`, `Infrastructure` e `Workers`.
- Manter classificação, acesso, orçamento, validação e segurança em código determinístico.
- Impedir vazamento entre tenants e envio de fontes não autorizadas ao Ollama.
- Cumprir e medir os SLAs iniciais: intent 300 ms, retrieval 800 ms, prompt 150 ms, primeiro token 3 s, validação 300 ms e total 10 s.
- Permitir substituição dos adaptadores externos em testes e evolução incremental posterior.

**Non-Goals:**

- Grafo, Redis, fusão/deduplicação avançada, tools de leitura ou escrita e workers operacionais.
- Classificação ou validação semântica por LLM, regeneração automática e streaming completo no MVP.
- Administração/editor de conhecimento e endpoint de feedback.

## Decisions

### 1. Estrutura por camada e área funcional

Um único projeto .NET, localizado em `C:\github.com\djifelipe\nexus_ai\AiGateway`, manterá os diretórios `Api`, `Application`, `Domain`, `Infrastructure` e `Workers`. Todos os caminhos citados neste design são relativos a essa raiz, salvo indicação explícita em contrário. Dentro deles serão criadas exatamente as áreas do tópico 11; contratos e regras puras ficam em `Domain`, casos de uso e portas em `Application`, transporte HTTP em `Api` e adaptadores externos em `Infrastructure`. `Workers` terá somente a estrutura reservada (`EmbeddingIndexer`, `KnowledgePublisher`, `FeedbackProcessor`) nesta fase. Nenhum código, teste, migração ou configuração da aplicação será criado ao lado de `openspec` na raiz `C:\github.com\djifelipe\nexus_ai`.

Alternativa considerada: projetos .NET separados por camada. Foi adiada para evitar complexidade de build prematura, mantendo limites por namespace e dependências direcionadas para posterior extração.

### 2. Identidade autenticada como autoridade

Um `UserContext` imutável será construído por middleware/filtro a partir das claims. Valores `companyId` e `userId` do payload serão validados contra esse contexto e nunca usados para ampliar acesso. As chamadas a `supabase-mcp-server_kb` exigirão explicitamente tenant, versão e permissões e deverão aplicar publicação, atividade e vigência no servidor antes de retornar conteúdo.

Alternativa considerada: confiar nos IDs do corpo e validar depois. Rejeitada porque permitiria consultas no tenant errado antes da autorização.

### 3. Orquestrador linear com portas explícitas

`IAiOrchestrator` coordena `IIntentRouter`, `IKnowledgeRetriever`, `IPromptBuilder`, `ILanguageModelClient`, `IResponseValidator` e `IAiTelemetry`. Cada porta recebe `CancellationToken`, retorna DTO tipado e não chama diretamente outra infraestrutura. Não haverá ciclo de tools: respostas do modelo que solicitem tools serão rejeitadas como não suportadas na Fase 1.

Alternativa considerada: pipeline de middleware genérico. Rejeitada no MVP porque obscureceria tipos, política de falhas e métricas específicas das etapas.

### 4. Intent Router determinístico e orientado a catálogo

O roteador normaliza texto, combina aliases/palavras-chave com contexto de tela e valida módulo, feature e ação no catálogo PostgreSQL. Pontuações são configuráveis e a estratégia vencedora é registrada. Confiança abaixo de 0,55 produz `Unknown`; ambiguidades sem contexto suficiente produzem `RequiresClarification`; entre 0,55 e 0,75 a recuperação pode consultar os módulos candidatos autorizados. Não há fallback LLM nesta fase.

### 5. Acesso segregado por MCP server e recuperação híbrida

O retriever executa buscas estruturada e vetorial exclusivamente por `IKnowledgeBaseMcpClient`, implementado pelo adaptador de `supabase-mcp-server_kb`. A aplicação não conhece connection string, não usa `NpgsqlDataSource` e não executa SQL diretamente. O request MCP transporta o escopo autenticado; tenant, versão, permissão, idioma, vigência, atividade e publicação são filtros obrigatórios executados no servidor antes do retorno. A geração de embedding permanece uma porta dedicada e a busca pgvector ocorre no servidor KB. Os candidatos são intercalados por ranking simples e estável, limitados por `MaxResults` (padrão 15) e `MaxContextTokens` (padrão 8000).

Uma porta separada `IErpMcpClient` identifica `supabase-mcp-server_ts` como único canal permitido para consultas futuras ao ERP. Ela não será registrada nem chamada na Fase 1. Nenhum dado retornado por KB pode ser enviado ao TS, ou vice-versa, sem caso de uso futuro explícito, autorização e sanitização.

Alternativa considerada: conexão direta com PostgreSQL/Supabase pela aplicação. Rejeitada por violar a segregação operacional e concentrar credenciais no gateway.

### 6. Prompt estruturado e defensivo

O builder produz mensagens separadas para sistema, contexto autorizado, intenção, conhecimento e pergunta. Fontes recebem delimitadores e IDs; conteúdo recuperado é explicitamente tratado como dado. Um estimador de tokens preserva regras críticas e a pergunta original, removendo primeiro fontes de menor prioridade. Padrões suspeitos de prompt injection são marcados/sanitizados sem transformar o builder em classificador semântico.

### 7. Ollama isolado por cliente resiliente

`Infrastructure/Ollama` implementará `ILanguageModelClient` via `HttpClientFactory`, URL/modelo/opções tipadas, timeout global compatível com o SLA, cancelamento e desserialização estrita. Respostas registram contagem de tokens informada pelo Ollama; ausência de contagem é marcada como indisponível, não estimada como fato. Erros de rede, timeout e formato geram códigos internos estáveis e uma resposta HTTP segura, sem stack trace.

### 8. Validação básica de citações

O validador extrai citações no formato `[source-id]`, compara-as com o conjunto exato enviado ao modelo e exige ao menos uma citação quando houver uma resposta factual fundamentada. Citação desconhecida impede o retorno do texto como `Grounded`; ausência de fontes ou de suporte produz `InsufficientKnowledge`. A validação de afirmações e regeneração ficam fora do MVP.

### 9. Observabilidade não bloqueante e sanitizada

Um escopo de telemetria mantém `requestId`, `traceId`, `conversationId`, tenant e usuário, mede cada etapa e tokens, e registra somente metadados necessários sobre fontes. `ISensitiveDataSanitizer` é aplicado a mensagens e erros. Falhas no sink são capturadas e não alteram a resposta principal. Métricas usam tags de cardinalidade controlada; IDs pessoais não viram labels.

### 10. Estratégia de testes

Testes unitários cobrirão regras, limiares, orçamento, prompt injection, citações e sanitização. Testes de integração usarão doubles/protocolo MCP e um ambiente de teste do `supabase-mcp-server_kb`, além de servidor Ollama simulado. Testes arquiteturais impedirão acesso PostgreSQL/Supabase direto e chamadas a `supabase-mcp-server_ts` durante a Fase 1.

## Risks / Trade-offs

- [Ranking simples reduzir relevância em consultas complexas] → registrar diagnósticos e preservar portas para fusão avançada na Fase 2.
- [Catálogo inicial incompleto aumenta intenções desconhecidas] → fornecer seed mínimo, métricas por módulo e comportamento explícito de esclarecimento.
- [Embeddings incompatíveis entre indexação e consulta] → persistir o identificador/dimensão do modelo e validar configuração na inicialização.
- [Ollama excede SLA ou fica indisponível] → timeout, cancelamento, erro seguro e métricas separadas; nenhuma repetição automática que ultrapasse o orçamento total.
- [Estimativa de tokens diverge do modelo] → margem configurável e uso da contagem real retornada para telemetria.
- [Logs vazam perguntas ou segredos] → sanitização central, logging estruturado por allowlist e testes com padrões sensíveis.
- [Estrutura em um único assembly permite acoplamento acidental] → namespaces, DI pelas portas e testes arquiteturais; separação física pode ocorrer depois.

## Migration Plan

1. Criar, a partir de `C:\github.com\djifelipe\nexus_ai\AiGateway`, a estrutura de pastas/namespaces e contratos de domínio/aplicação sem alterar o endpoint atual.
2. Adicionar configuração validada do MCP KB e migrações para catálogo, documentos/chunks e pgvector; aplicar somente por `supabase-mcp-server_kb`.
3. Registrar adaptadores MCP KB, embedding, Ollama e observabilidade; manter o MCP TS não registrado na Fase 1.
4. Publicar o endpoint protegido por configuração/feature flag, executar smoke tests e medir SLAs com carga controlada.
5. Habilitar gradualmente por tenant. Em rollback, desabilitar o endpoint/flag e reverter a aplicação; manter tabelas aditivas para evitar perda de conhecimento.

## Open Questions

- Modelo padronizado para a Fase 1: `nomic-embed-text`, com 768 dimensões, compatível com índice HNSW do pgvector.
- Quais claims existentes representam tenant, versão do ERP e permissões?
- O Ollama de produção suporta streaming e métricas de primeiro token no protocolo escolhido, ou o MVP medirá apenas a resposta completa?
