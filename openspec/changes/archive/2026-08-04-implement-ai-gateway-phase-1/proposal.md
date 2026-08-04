## Why

O projeto possui apenas o esqueleto da API e precisa de um primeiro fluxo ponta a ponta capaz de responder perguntas sobre o ERP com fontes rastreáveis, isolamento de tenant e controles determinísticos. A Fase 1 estabelece esse MVP fundamentado e a base arquitetural sobre a qual as fases posteriores serão adicionadas.

## What Changes

- Implementar `POST /api/ai/chat` e o orquestrador do fluxo de pergunta, intenção, recuperação, prompt, geração, validação e resposta.
- Estruturar o código sob a raiz obrigatória `C:\github.com\djifelipe\nexus_ai\AiGateway`, conforme o tópico 11, separando `Api`, `Application`, `Domain`, `Infrastructure` e `Workers`, com os componentes da Fase 1 em suas respectivas camadas.
- Implementar roteamento de intenção baseado em regras e catálogo, incluindo tratamento de baixa confiança e ambiguidade, sem fallback por LLM nesta fase.
- Implementar recuperação híbrida em PostgreSQL e pgvector exclusivamente pelo MCP server `supabase-mcp-server_kb`, com orçamento de resultados/contexto e filtros obrigatórios de tenant, versão, permissão, vigência e publicação.
- Estabelecer `supabase-mcp-server_ts` como único canal permitido para dados transacionais ou cadastrais do ERP; ele não será chamado na Fase 1 porque tools e consultas ao ERP permanecem fora do MVP.
- Implementar construção de prompt com fontes identificadas, priorização, limite de tokens e separação entre instruções e conteúdo recuperado.
- Integrar o Ollama para geração de respostas, com timeout, cancelamento, tratamento de indisponibilidade e contabilização de tokens.
- Implementar validação básica determinística de citações e resposta segura para conhecimento insuficiente ou citações inválidas.
- Registrar identificadores de correlação, latências por etapa, tokens, erros e fontes utilizadas, sanitizando dados sensíveis e sem tornar a telemetria crítica para a resposta.
- Proibir ferramentas e qualquer operação de escrita no MVP; tools, grafo, cache, validação semântica avançada e ações controladas permanecem fora do escopo.

## Capabilities

### New Capabilities

- `ai-chat-orchestration`: Contrato HTTP e coordenação determinística do fluxo completo do chat da Fase 1.
- `rule-based-intent-routing`: Classificação de intenção por regras e catálogo, com confiança e esclarecimento de ambiguidades.
- `hybrid-knowledge-retrieval`: Recuperação SQL e pgvector via `supabase-mcp-server_kb`, com isolamento, autorização, publicação e orçamento de contexto.
- `grounded-prompt-building`: Montagem segura e limitada do prompt a partir de intenção, contexto e fontes rastreáveis.
- `ollama-generation`: Comunicação resiliente com o Ollama e retorno de conteúdo e uso de tokens.
- `citation-validation`: Verificação determinística de que as citações da resposta pertencem às fontes fornecidas.
- `ai-request-telemetry`: Correlação, latências, tokens, erros e rastreabilidade das fontes com sanitização e tolerância a falhas.

### Modified Capabilities

Nenhuma. Não existem especificações principais anteriores neste repositório.

## Impact

- **Raiz da aplicação:** todo código-fonte, configuração, migração, teste e documentação operacional deve ser criado dentro de `C:\github.com\djifelipe\nexus_ai\AiGateway`. A pasta `C:\github.com\djifelipe\nexus_ai\openspec` permanece reservada aos artefatos OpenSpec e não é a raiz do código da aplicação.
- **Código:** criação, relativamente à raiz `AiGateway`, das áreas de Api, Application e Domain, além de adaptadores em `Infrastructure/Ollama`, `Infrastructure/Mcp/KnowledgeBase`, `Infrastructure/Mcp/Erp`, `Infrastructure/Observability` e `Infrastructure/Security`.
- **API:** novo `POST /api/ai/chat`, com identidade efetiva derivada do contexto autenticado; `companyId` e `userId` do payload não podem ampliar o acesso.
- **Dados e dependências:** cliente MCP para `supabase-mcp-server_kb`, contrato reservado para `supabase-mcp-server_ts`, cliente HTTP para Ollama, opções tipadas e instrumentação. Migrações de conhecimento devem ser aplicadas exclusivamente pelo MCP server KB.
- **Separação de bases:** a aplicação não recebe connection strings de conhecimento ou ERP, não abre conexões PostgreSQL diretas e não substitui um MCP server pelo outro. KB atende somente conhecimento; TS atende somente ERP.
- **Segurança e privacidade:** filtros de tenant e permissão são aplicados antes de o conteúdo alcançar o modelo; versão, vigência e publicação também limitam a recuperação. Perguntas, logs e erros são sanitizados e não expõem segredos ou dados de outro tenant.
- **Telemetria:** cada requisição propaga `requestId`, `traceId` e `conversationId`, mede os SLAs da Fase 1 e registra tokens/fontes sem bloquear a resposta se o backend de observabilidade falhar.
- **Não objetivos:** expansão por grafo, fusão avançada de scores, deduplicação semântica, cache, tools de leitura, validação semântica por afirmações, regeneração automática e qualquer tool/operação de escrita.
