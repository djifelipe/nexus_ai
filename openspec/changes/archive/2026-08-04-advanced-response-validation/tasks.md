## 1. Contratos e configuração de domínio

- [x] 1.1 Criar em `AiGateway/Domain/Responses` os contratos imutáveis de afirmação, evidência, resultado por afirmação, componentes do score, motivo sanitizado e resultado avançado, preservando os status públicos existentes.
- [x] 1.2 Criar em `AiGateway/Domain/Policies` a política de validação com pesos 0,35/0,25/0,25/0,15, limiares, categorias bloqueantes, limites de resposta/afirmações/candidatos e versão da política.
- [x] 1.3 Adicionar opções tipadas para timeouts, limites, feature flags de modo sombra/regeneração e validar configurações inválidas na inicialização.
- [x] 1.4 Evoluir `IResponseValidator` e os contratos do orquestrador para receber fontes exatas do prompt, intenção, usuário autenticado, permissões e cancellation token sem alterar incompativelmente o contrato HTTP.

## 2. Extração e fundamentação de afirmações

- [x] 2.1 Implementar em `AiGateway/Application/Validation` o extrator determinístico de afirmações com IDs estáveis, spans, classificação factual e limites configurados.
- [x] 2.2 Implementar a abstração opcional do extrator por modelo com JSON Schema, timeout, cancelamento, validação de saída e fallback conservador para `RequiresReview`.
- [x] 2.3 Implementar seleção de evidências exclusivamente a partir das fontes autorizadas incluídas no `PromptPackage`, mantendo IDs e metadados de tenant, versão, permissão e publicação.
- [x] 2.4 Implementar verificação semântica por abstração de embeddings/avaliador, com candidatos limitados, score por afirmação, detecção de contradição e falhas externas mapeadas para códigos sanitizados.
- [x] 2.5 Garantir que qualquer integração de embeddings/conhecimento reutilize apenas abstrações MCP autorizadas e não introduza conexão direta a PostgreSQL, pgvector ou banco do ERP.

## 3. Segurança, citações e decisão final

- [x] 3.1 Evoluir a validação de citações para associar citações válidas às afirmações e rejeitar citações existentes que não sustentem o conteúdo alegado.
- [x] 3.2 Estender `AiGateway/Infrastructure/Security` com detectores categorizados de credenciais, tokens, connection strings, prompts internos, SQL, stack traces e dados pessoais, bancários, fiscais ou cross-tenant.
- [x] 3.3 Implementar em Application as políticas determinísticas de permissão e bloqueio, garantindo que texto sensível bruto nunca apareça na resposta, nos motivos ou na observabilidade.
- [x] 3.4 Implementar o agregador do score normalizado e a precedência de `Unsafe`, `InsufficientKnowledge`, `InvalidFormat`, `RequiresReview`, `PartiallyGrounded` e `Grounded` sobre limiares numéricos.
- [x] 3.5 Compor os estágios no validador avançado com limites, timeout, cancelamento e resposta segura para excesso de tamanho, afirmações ou candidatos.

## 4. Regeneração e orquestração

- [x] 4.1 Implementar a política que classifica apenas `PartiallyGrounded` e `InvalidFormat` corrigíveis como elegíveis à regeneração, excluindo `Unsafe`, acesso negado, conhecimento insuficiente e falha externa.
- [x] 4.2 Atualizar `AiGateway/Application/Orchestration` para executar no máximo uma regeneração com pergunta, identidade autenticada, intenção, fontes e políticas originais mais feedback estritamente sanitizado.
- [x] 4.3 Reexecutar o pipeline completo sobre a resposta regenerada e retornar resultado seguro terminal quando a segunda validação reprovar, sem ampliar fontes, permissões ou chamadas de tools.
- [x] 4.4 Adicionar modo sombra e feature flags para habilitar progressivamente decisão avançada e regeneração, preservando rollback para o validador de citações básico.

## 5. Telemetria e privacidade

- [x] 5.1 Instrumentar `ai.response.validate` com duração, status, faixa do score, versão da política e contagens limitadas de afirmações/citações, sem conteúdo bruto ou identificadores não limitados em labels.
- [x] 5.2 Registrar tentativa inicial, categoria sanitizada do gatilho, contador de regeneração limitado a um e resultado terminal correlacionados por request/trace/conversation.
- [x] 5.3 Garantir que falhas do sink de observabilidade sejam não bloqueantes e que falhas de dependências semânticas emitam apenas códigos estáveis sanitizados.
- [x] 5.4 Atualizar documentação operacional sob `AiGateway` com opções, limites, timeouts, modo sombra, ativação gradual, métricas e procedimento de rollback.

## 6. Testes e verificação

- [x] 6.1 Criar testes unitários para extração determinística/model-based, associação de citações, grounding/contradição, fórmula do score, precedência de status e limites.
- [x] 6.2 Criar testes de segurança para secrets, prompt injection, SQL/stack trace, PII e instruções incompatíveis com permissões, comprovando ausência do valor bruto em resposta, motivos e telemetria.
- [x] 6.3 Criar testes de integração para resposta grounded, parcialmente grounded, conhecimento insuficiente, acesso negado, falha/timeout externo, cancelamento e uma única regeneração.
- [x] 6.4 Criar testes de isolamento que tentem usar evidência de outro tenant, versão, permissão ou estado de publicação e comprovem que ela não participa da validação nem vaza na observabilidade.
- [x] 6.5 Criar testes de observabilidade não bloqueante e de cardinalidade/conteúdo permitido para validação e regeneração.
- [x] 6.6 Executar benchmark do conjunto de aceite, registrar separadamente latência básica e avançada e ajustar limites sem violar o SLA global de resposta configurado.
- [x] 6.7 Executar `dotnet build` e `dotnet test` no projeto sob `AiGateway` e corrigir todas as falhas.
- [x] 6.8 Validar todos os cenários das delta specs, confirmar que nenhum artefato de aplicação foi criado fora de `AiGateway` e executar `openspec validate advanced-response-validation --strict`.

