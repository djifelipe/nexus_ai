## Why

A Fase 1 recupera conhecimento por consulta estruturada e similaridade vetorial, mas ainda não explora relações do domínio, não consolida adequadamente resultados concorrentes e repete trabalho entre requisições equivalentes. A Fase 2 deve elevar relevância, segurança e desempenho da recuperação sem ampliar o acesso a dados nem transferir decisões de autorização ao LLM.

## What Changes

- Adicionar expansão limitada por grafo a partir das entidades, features, ações e fontes identificadas, com profundidade padrão 2 e máxima 4.
- Fundir resultados estruturados, vetoriais e de grafo com pesos configuráveis por tipo de intenção, preservando scores e explicações para auditoria.
- Deduplicar chunks redundantes, versões obsoletas e itens semanticamente equivalentes antes da montagem do contexto.
- Consolidar a aplicação determinística de tenant, versão do ERP, permissões, idioma, vigência, publicação e tipo de conteúdo em todas as estratégias de recuperação.
- Adicionar cache de busca e de resposta com chaves que incluam tenant, versão, permissões efetivas, intenção, consulta normalizada e revisões das fontes; impedir compartilhamento entre contextos de acesso distintos.
- Invalidar ou ignorar entradas de cache quando conteúdo, permissões, versão, vigência ou publicação mudarem, e operar corretamente quando Redis estiver indisponível.
- Ampliar diagnósticos e telemetria com contribuições por estratégia, decisões de deduplicação, filtros, expansão do grafo e hits/misses/invalidações de cache.
- Estruturar contratos e políticas em `Domain`, orquestração em `Application/Retrieval`, adaptadores de grafo/cache/KB MCP em `Infrastructure/Graph`, `Infrastructure/Redis` e infraestrutura do MCP, mantendo a API compatível.
- Manter fora do escopo ferramentas de leitura/escrita sobre dados transacionais, validação avançada de respostas e qualquer conexão direta com PostgreSQL, pgvector ou banco do ERP.

## Capabilities

### New Capabilities

- `knowledge-graph-expansion`: Expansão autorizada e limitada de relações de conhecimento, com caminhos rastreáveis, limites e comportamento controlado de falha.
- `retrieval-response-caching`: Cache seguro de buscas e respostas, isolado por contexto de acesso, versionado, invalidável e tolerante à indisponibilidade do Redis.

### Modified Capabilities

- `hybrid-knowledge-retrieval`: Evoluir o contrato de recuperação para fusão configurável de SQL, vetor e grafo, deduplicação semântica e aplicação consistente de filtros de versão e permissão antes de ranking, cache e prompt.

## Impact

- Afeta principalmente `AiGateway/Application/Retrieval`, `AiGateway/Domain/Knowledge`, `AiGateway/Domain/Policies`, `AiGateway/Infrastructure/Graph`, `AiGateway/Infrastructure/Redis`, o adaptador de `supabase-mcp-server_kb`, configuração, telemetria e testes.
- Estende `RetrievalResult`, `KnowledgeItem`, `GraphPath` e diagnósticos sem alterar o contrato HTTP obrigatório de `POST /api/ai/chat`.
- Introduz dependências de infraestrutura para grafo e Redis, ambas encapsuladas por interfaces e com falha controlada; o KB continua acessível exclusivamente via `supabase-mcp-server_kb`.
- Preserva isolamento de tenant, autorização determinística, privacidade e sanitização; chaves e métricas de cache não devem expor pergunta bruta, conteúdo, credenciais ou dados pessoais.
- O SLA-alvo do Knowledge Retriever permanece em até 800 ms no conjunto de aceite, acompanhado por spans das estratégias, fusão, deduplicação e cache.
