## Why

A fase 3 precisa permitir que o AI Gateway responda perguntas operacionais com dados atuais do ERP, sem ampliar o risco para operações de escrita. A mudança introduz ferramentas somente leitura com contratos determinísticos, isolamento por tenant, autorização, sanitização e auditoria, mantendo o modelo apenas como solicitante de ferramentas previamente registradas.

## What Changes

- Adicionar um catálogo e um executor de ferramentas somente leitura para `inventory.getBalance`, `invoice.getStatus`, `permission.check`, `workflow.get` e `customer.getSummary`.
- Definir contratos de entrada e saída validados por JSON Schema, erros estáveis e resultados mínimos/sanitizados para cada ferramenta.
- Encaminhar consultas transacionais e cadastrais exclusivamente ao `supabase-mcp-server_ts`; `workflow.get` consulta conteúdo da base de conhecimento exclusivamente pelo `supabase-mcp-server_kb`.
- Derivar empresa, usuário e permissões do contexto autenticado, rejeitando ferramentas não registradas, parâmetros inválidos, acesso entre tenants e permissões insuficientes.
- Integrar o ciclo de chamadas de ferramenta ao `AiOrchestrator`, limitado a cinco chamadas por requisição, duas repetições da mesma ferramenta e timeout individual de dez segundos.
- Registrar execução, duração, usuário, ferramenta, resultado sanitizado e código de erro, sem persistir payloads sensíveis ou transformar indisponibilidade de telemetria em falha da resposta.
- Manter toda operação de escrita fora do escopo da fase 3; nenhuma ferramenta desta mudança solicita confirmação, persiste mutações ou aceita SQL arbitrário.
- Organizar contratos em `Domain/Tools`, coordenação e validação em `Application/Tools`, adaptadores MCP em `Infrastructure`, integração em `Application/Orchestration` e testes sob `AiGateway/Tests`, conforme a estrutura do tópico 11.

## Capabilities

### New Capabilities

- `read-only-tool-execution`: Registro, autorização, validação, execução e sanitização das cinco ferramentas somente leitura da fase 3 por meio dos MCP servers designados.

### Modified Capabilities

- `ai-chat-orchestration`: Substitui a recusa geral de ferramentas da fase 1 por um ciclo limitado que executa apenas ferramentas somente leitura autorizadas e devolve seus resultados ao modelo.
- `rule-based-intent-routing`: Passa a declarar ferramentas requeridas somente a partir do catálogo autorizado para intenções de consulta de dados, permissões e workflow.
- `ai-request-telemetry`: Passa a medir e auditar chamadas de ferramenta com correlação e sanitização, sem labels de alta cardinalidade nem conteúdo sensível.

## Impact

- Código afetado sob `AiGateway`: `Api`, `Application/Orchestration`, `Application/IntentRouting`, `Application/Tools`, `Application/Telemetry`, `Domain/Tools`, `Domain/Policies`, `Infrastructure` e `Tests`.
- Contratos afetados: resultado do roteamento (`RequiredTools`), resposta do cliente Ollama com tool calls, `IToolExecutor`, definições/solicitações/resultados de ferramentas, códigos de erro e eventos de telemetria.
- Sistemas externos: `supabase-mcp-server_ts` para estoque, situação documental, permissões e dados cadastrais; `supabase-mcp-server_kb` para workflow. Não haverá conexão direta, credencial de banco ou SQL arbitrário na aplicação.
- Segurança e privacidade: tenant e usuário vêm exclusivamente do contexto autenticado; autorização é determinística; respostas e auditoria são minimizadas e sanitizadas; operações de escrita continuam proibidas.
