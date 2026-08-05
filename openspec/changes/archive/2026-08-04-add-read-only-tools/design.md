## Context

As fases 1 e 2 produzem respostas fundamentadas em conhecimento, mas ainda recusam qualquer tool call. A fase 3 acrescenta dados operacionais atuais sem permitir mutações. O limite arquitetural é explícito: código da aplicação permanece sob `AiGateway` e segue as camadas do tópico 11; conhecimento e workflow usam somente `supabase-mcp-server_kb`; dados transacionais e cadastrais usam somente `supabase-mcp-server_ts`. O LLM não decide identidade, autorização, risco nem destino de dados.

## Goals / Non-Goals

**Goals:**

- Executar as cinco ferramentas da fase 3 com contratos estáveis, tipados e testáveis.
- Isolar tenant e usuário, validar permissões antes do acesso e minimizar dados retornados ao LLM.
- Integrar tool calls ao orquestrador com limites, timeout, cancelamento, auditoria e falhas seguras.
- Distribuir responsabilidades entre `Domain`, `Application`, `Infrastructure` e `Api` conforme o tópico 11.

**Non-Goals:**

- Criar ferramentas de escrita, confirmação, idempotência, simulação ou rollback.
- Aceitar SQL, nomes de ferramentas ou identidade de tenant definidos livremente pelo modelo.
- Conectar a aplicação diretamente aos bancos ou misturar resultados dos dois MCP servers em uma única ferramenta.
- Implementar a validação semântica avançada da fase 4.

## Decisions

### 1. Catálogo fechado e contratos no domínio

`Domain/Tools` conterá `ToolDefinition`, `ToolRiskLevel.ReadOnly`, envelopes de solicitação/resultado e códigos de erro. Cada definição terá nome constante, descrição, JSON Schema, permissões exigidas e política de resultado. `Application/Tools` terá registro imutável e rejeitará qualquer nome ausente.

Alternativa considerada: refletir métodos ou aceitar nomes fornecidos pelo LLM. Rejeitada porque amplia a superfície de execução e impede revisão explícita do catálogo.

### 2. Um handler por ferramenta e uma fronteira MCP por origem

Handlers de aplicação implementarão um contrato comum e dependerão de portas específicas. Adaptadores em `Infrastructure` mapearão estoque, documento, permissão e cliente exclusivamente para `supabase-mcp-server_ts`; workflow será mapeado exclusivamente para `supabase-mcp-server_kb`. Os adaptadores aceitarão consultas parametrizadas/allowlisted, nunca SQL arbitrário.

Contratos funcionais:

- `inventory.getBalance`: identificador de produto e, opcionalmente, estabelecimento/depósito; retorna saldo disponível e unidade.
- `invoice.getStatus`: tipo e identificador do documento; retorna status, datas e motivos não sensíveis.
- `permission.check`: código de permissão; retorna decisão e escopo, sem revelar políticas internas desnecessárias.
- `workflow.get`: módulo, feature e ação catalogados; retorna passos publicados e autorizados, versão e source ID.
- `customer.getSummary`: identificador cadastral; retorna apenas campos resumidos allowlisted, vedando dados bancários/fiscais completos.

Alternativa considerada: handler genérico com consulta livre. Rejeitada por dificultar autorização, sanitização e testes por ferramenta.

### 3. Contexto autenticado prevalece sobre argumentos

O executor receberá `ToolExecutionRequest` com contexto autenticado separado dos argumentos do modelo. `companyId`, `userId` e permissões efetivos nunca serão lidos somente do JSON da tool call. Argumentos de identidade conflitantes serão rejeitados antes do MCP. Políticas determinísticas validarão permissão exigida e escopo de tenant.

Alternativa considerada: remover silenciosamente identidades conflitantes. Rejeitada porque ocultaria uma tentativa incorreta ou maliciosa e prejudicaria a auditoria.

### 4. Pipeline de execução seguro

O fluxo será: resolver definição; validar JSON Schema; vincular contexto autenticado; autorizar; aplicar timeout vinculado de dez segundos; executar handler; sanitizar/minimizar; auditar; devolver resultado tipado. O orçamento por requisição será controlado pelo orquestrador: no máximo cinco execuções e duas do mesmo nome. Cancelamento do cliente interromperá chamadas pendentes.

Falhas serão mapeadas para códigos estáveis (`tool_not_registered`, `invalid_arguments`, `access_denied`, `not_found`, `timeout`, `dependency_unavailable`, `result_rejected`) sem stack trace ou detalhe do MCP. Não haverá retry automático na fase 3, evitando multiplicar carga e latência sem uma política específica.

### 5. Ciclo de tools no orquestrador

Depois da primeira resposta do Ollama, o `AiOrchestrator` executará tool calls autorizadas, anexará resultados como dados não confiáveis e fará nova chamada ao modelo. O ciclo termina quando não houver tool call, quando atingir um limite ou quando ocorrer falha terminal. Solicitações de escrita e nomes desconhecidos serão recusados, não reinterpretados.

O Intent Router poderá preencher `RequiredTools` apenas com nomes existentes no catálogo; essa indicação auxilia o prompt, mas não substitui a validação de cada chamada efetiva.

### 6. Auditoria e telemetria minimizadas

Cada tentativa produzirá span `ai.tool.execute` e métrica de duração/contagem, correlacionados por request/trace/conversation. Auditoria registrará nome, risco, usuário, empresa, sucesso, duração e código de erro; argumentos e resultados serão representados apenas por campos allowlisted ou hashes quando necessário. IDs de usuário/empresa não serão labels de métricas. Falha do sink não alterará uma resposta válida.

### 7. Estratégia de testes

Testes unitários cobrirão catálogo, schema, autorização, limites, sanitização e mapeamento de erros. Testes de integração usarão doubles contratuais dos dois MCP servers para provar roteamento correto e ausência de chamada em falha prévia. Testes de orquestração cobrirão sucesso, múltiplas calls, loops, timeout, cancelamento e tentativa de escrita. Testes de arquitetura verificarão que artefatos permanecem sob `AiGateway` e que Application não referencia clientes MCP concretos.

## Risks / Trade-offs

- [Schemas ou respostas do ERP divergirem entre ambientes] → Isolar mapeamento nos adaptadores, validar respostas e usar testes contratuais versionados.
- [Resultado de ferramenta conter dado sensível] → Projeção allowlisted por handler seguida de sanitização central e rejeição segura.
- [Modelo criar loops ou chamadas custosas] → Limites globais e por nome, timeout, cancelamento e término controlado.
- [Permissões mudarem durante a conversa] → Autorizar novamente cada execução com o contexto atual, sem confiar em tool results anteriores.
- [MCP indisponível aumentar latência] → Timeout individual, erro estável e telemetria; não prometer o SLA sem tools para solicitações com tools.
- [Workflow publicado e dado transacional produzirem informações conflitantes] → Manter proveniência e finalidade separadas; o validador trata o tool result como dado atual e o workflow como fonte documental.

## Migration Plan

1. Introduzir contratos, catálogo e políticas com todas as ferramentas desabilitadas por configuração.
2. Adicionar portas, adaptadores MCP, handlers e testes contratuais por origem de dados.
3. Integrar o executor e a telemetria ao orquestrador, preservando a recusa de escrita.
4. Habilitar gradualmente cada ferramenta por ambiente e tenant após validação de permissões e sanitização.
5. Em rollback, desabilitar o catálogo da fase 3; o pipeline volta ao comportamento de resposta sem tools, sem migração destrutiva de dados.

## Open Questions

- Quais nomes exatos das operações expostas por cada MCP server serão vinculados às cinco portas?
- Quais campos cadastrais compõem a allowlist final de `customer.getSummary` em cada versão do ERP?
- Quais códigos de permissão do ERP autorizam cada ferramenta e seus filtros opcionais?

