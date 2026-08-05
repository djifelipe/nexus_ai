## Why

A validação atual garante citações básicas, mas ainda pode permitir afirmações sem suporte, dados sensíveis ou respostas parcialmente fundamentadas. A Fase 4 é necessária para tornar a decisão de confiança verificável e permitir uma única tentativa controlada de correção antes de bloquear ou degradar a resposta.

## What Changes

- Extrair afirmações verificáveis da resposta e relacioná-las às fontes autorizadas usadas na geração.
- Verificar semanticamente cada afirmação, sem usar o LLM avaliador como mecanismo exclusivo de decisão.
- Calcular um score de fundamentação a partir de cobertura de recuperação, citações, suporte semântico e confiança da intenção.
- Detectar e bloquear dados sensíveis, segredos, conteúdo de outro tenant e instruções incompatíveis com permissões.
- Permitir no máximo uma regeneração automática para respostas parcialmente fundamentadas ou com formato corrigível, preservando contexto, tenant, fontes e políticas originais.
- Produzir motivos sanitizados, warnings e telemetria para aprovação, correção, rejeição e falhas externas.
- Manter fora do escopo ferramentas de escrita, alteração de dados, revisão humana completa e decisões de acesso delegadas exclusivamente ao modelo.

## Capabilities

### New Capabilities
- `advanced-response-validation`: Extração e verificação de afirmações, proteção contra dados sensíveis, score de fundamentação e decisão final determinística da resposta.

### Modified Capabilities
- `citation-validation`: Integra citações válidas à avaliação por afirmação e distingue respostas fundamentadas, parcialmente fundamentadas, insuficientes, inseguras e inválidas.
- `ai-chat-orchestration`: Adiciona uma única regeneração controlada e revalidação antes da resposta terminal.
- `ai-request-telemetry`: Registra métricas e eventos sanitizados da validação avançada, score, afirmações sem suporte e regeneração.

## Impact

- Código em `AiGateway/Application/Validation` para o pipeline e as políticas de validação, `AiGateway/Domain/Responses` e `AiGateway/Domain/Policies` para contratos e decisões, `AiGateway/Infrastructure/Security` para detectores/sanitização e `AiGateway/Application/Orchestration` para regeneração e revalidação.
- Contratos de validação e resposta passam a expor status, confidence, warnings e razões sanitizadas consistentes; o contrato HTTP permanece compatível.
- Integrações de embeddings e LLM avaliador ficam atrás de abstrações, com timeout, cancelamento e fallback determinístico. Nenhum acesso direto a PostgreSQL/pgvector ou ao ERP será introduzido.
- Logs, traces e métricas ganham resultados agregados da validação sem registrar resposta bruta, afirmações sensíveis, prompts, credenciais ou identificadores de tenant como labels não limitadas.
- Testes cobrirão fundamentação, conhecimento insuficiente, permissões, isolamento entre tenants, dados sensíveis, falhas externas, limite de regeneração e observabilidade não bloqueante.
