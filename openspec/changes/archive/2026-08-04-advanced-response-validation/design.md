## Context

O gateway já possui validação determinística de citações, sanitização de dados sensíveis e uma orquestração que encerra após a primeira validação. A Fase 4 amplia esse fluxo para avaliar afirmações individuais, combinar sinais de confiança e, quando seguro, regenerar uma única vez. A solução atravessa `Application/Validation`, `Application/Orchestration`, `Domain/Responses`, `Domain/Policies`, `Infrastructure/Security` e observabilidade, preservando a organização do tópico 11 e mantendo todo código sob `AiGateway`.

## Goals / Non-Goals

**Goals:**

- Produzir uma decisão de validação explicável por afirmação e por fonte autorizada.
- Detectar conteúdo sensível e violações de permissão antes de qualquer resposta ao cliente.
- Calcular confidence por fórmula configurável e determinística, sem aceitar a autoavaliação do LLM como fonte única.
- Corrigir respostas recuperáveis com no máximo uma regeneração e uma nova validação completa.
- Manter falhas de dependências, logs e telemetria seguras, limitadas e não bloqueantes quando aplicável.

**Non-Goals:**

- Introduzir ferramentas de escrita, confirmação, rollback ou alterações no ERP.
- Reconsultar conhecimento sem filtros originais ou acessar diretamente PostgreSQL/pgvector.
- Implementar fila de revisão humana ou novo endpoint público.
- Garantir verdade universal além das fontes autorizadas fornecidas ao modelo.

## Decisions

### Pipeline composto em `Application/Validation`

`IResponseValidator` coordenará validadores especializados: estrutura/citações, extração de afirmações, grounding semântico, políticas/permissões, dados sensíveis e agregação do resultado. Os contratos imutáveis ficarão em `Domain/Responses` e as políticas/limiares em `Domain/Policies`. Isso mantém o caso de uso na camada Application e impede que infraestrutura dite a decisão final. A alternativa de um único prompt avaliador foi rejeitada por baixa previsibilidade e por delegar segurança ao LLM.

Fluxo:

```text
Resposta + fontes autorizadas + intenção + usuário
  -> validações determinísticas (formato, citações, segurança, permissão)
  -> extração de afirmações
  -> evidência candidata somente no conjunto autorizado
  -> similaridade semântica e avaliador opcional
  -> score e decisão determinística
  -> retorno | uma regeneração | bloqueio
```

### Extração híbrida de afirmações

Um extrator determinístico separará sentenças e descartará conteúdo não factual; um extrator baseado em modelo poderá complementar casos complexos por meio de saída JSON estruturada, limite de tamanho, timeout e cancelamento. Saída inválida ou indisponibilidade do modelo cairá no extrator determinístico e, se a cobertura não for segura, resultará em `RequiresReview`, nunca em aprovação implícita.

### Grounding restrito às fontes do prompt

Cada afirmação será comparada apenas com fontes efetivamente autorizadas e incluídas no `PromptPackage`, preservando IDs, tenant, versão, permissão e estado de publicação já validados pela recuperação. Embeddings necessários serão obtidos pela abstração de infraestrutura existente; não haverá conexão direta ao banco. O LLM avaliador é sinal complementar e sua saída será validada por schema.

### Score e classificação determinísticos

O score seguirá a fórmula inicial do tópico 8.6: `retrievalCoverage * 0.35 + citationCoverage * 0.25 + semanticGrounding * 0.25 + intentConfidence * 0.15`, com componentes normalizados entre 0 e 1 e limiares configuráveis/validados na inicialização. Violações de segurança ou acesso têm precedência e sempre produzem `Unsafe`; ausência de evidência suficiente produz `InsufficientKnowledge`; suporte parcial produz `PartiallyGrounded` ou `RequiresReview`. O score não pode elevar uma resposta que falhou em uma regra mandatória.

### Regeneração única e controlada

O orquestrador regenerará somente `PartiallyGrounded` ou `InvalidFormat` quando a política indicar que a falha é corrigível. Ele reutilizará pergunta, intenção, fontes autorizadas, identidade autenticada e políticas, acrescentando apenas razões sanitizadas e IDs de afirmações — nunca dados sensíveis ou prompts internos. A resposta regenerada passa por todo o pipeline e o contador máximo é um. `Unsafe`, `InsufficientKnowledge`, falha externa e segunda reprovação terminam com resposta segura.

### Detecção de dados sensíveis em camadas

`Infrastructure/Security` fornecerá detectores por allowlist/regex e classificadores extensíveis para credenciais, tokens, connection strings, dados pessoais, bancários e fiscais. A decisão de bloqueio permanece em `Application/Validation`. Texto bruto detectado não será incluído em razões, logs ou telemetria; somente categoria, posição segura/mascarada e código estável. A alternativa de apenas sanitizar após a decisão foi rejeitada porque poderia devolver uma resposta semanticamente comprometida.

### Observabilidade limitada

O span `ai.response.validate` registrará duração, status, score agregado, contagens de afirmações e citações, categorias de falha e se houve regeneração. Nenhuma resposta, afirmação, evidência, prompt ou identificador de tenant será label de métrica. Falhas do sink não alterarão a decisão, mas falhas de um validador externo essencial resultarão em estado conservador e código sanitizado.

### Limites e testes

Tamanho máximo da resposta, número de afirmações, candidatos por afirmação, timeouts de embedding/avaliador e limiares serão opções validadas. Testes unitários cobrirão cada estágio; integração cobrirá orquestração e falhas; testes de segurança cobrirão tenant, permissão, secrets e prompt injection; testes de desempenho registrarão a latência separada da validação básica e avançada.

## Risks / Trade-offs

- [Similaridade semântica pode produzir falso positivo] → exigir citação/evidência rastreável, limiar conservador e estado `RequiresReview` na zona cinzenta.
- [Regeneração aumenta latência e custo] → limitar a uma tentativa, registrar métricas e não regenerar falhas não corrigíveis.
- [Detecção de PII pode mascarar conteúdo legítimo] → categorias configuráveis, testes com dados ERP e razões auditáveis sem conteúdo bruto.
- [Falha do avaliador externo reduz cobertura] → fallback determinístico e decisão conservadora; nunca aprovar por ausência do avaliador.
- [Muitas afirmações ampliam custo] → impor limites de resposta/candidatos e encerrar com resultado seguro quando excedidos.
- [Mudança de limiares altera classificação] → validar configuração, versionar política em telemetria e oferecer rollback por feature flag.

## Migration Plan

1. Introduzir contratos e componentes com a validação avançada desabilitada por padrão.
2. Executar em modo sombra para comparar decisões sem alterar respostas.
3. Habilitar score e bloqueio de dados sensíveis; monitorar falsos positivos e latência.
4. Habilitar regeneração única por configuração após validar métricas.
5. Para rollback, desabilitar a feature flag e retornar ao validador de citações existente, preservando os novos eventos como opcionais.

## Open Questions

- Quais limiares de `Grounded`, `PartiallyGrounded` e `RequiresReview` serão calibrados com o conjunto real de avaliação?
- Quais categorias fiscais e pessoais podem aparecer legitimamente na resposta e exigem mascaramento em vez de bloqueio?
- O avaliador semântico inicial reutilizará o Ollama configurado ou um modelo separado com SLA próprio?
