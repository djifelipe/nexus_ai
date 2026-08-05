using System.Diagnostics;
using AiGateway.Domain;
using AiGateway.Domain.Tools;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace AiGateway.Application.Orchestration;

public sealed class AiOrchestrator(IIntentRouter intents, IKnowledgeRetriever retrieval, IPromptBuilder prompts, ILanguageModelClient model, IResponseValidator validator, IAiTelemetry telemetry, IOptions<AiGatewayOptions> options, IOptions<AdvancedRetrievalOptions> advanced, IResponseCache responseCache, ICacheKeyFactory cacheKeys, AiGateway.Domain.Policies.ICacheAdmissionPolicy cacheAdmission, ISensitiveDataSanitizer sanitizer, IToolCatalog toolCatalog, IToolExecutor toolExecutor, IOptions<ReadOnlyToolsOptions> toolOptions) : IAiOrchestrator
{
    public async Task<AiResponse> ExecuteAsync(AiRequest request, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TotalTimeoutSeconds)); using var operation = telemetry.StartRequest(request); var total = Stopwatch.StartNew();
        try
        {
            var (intent, intentMs) = await Timed("intent.route", () => intents.RouteAsync(new(request.Message, request.UserContext), timeout.Token));
            if (intent.RequiresClarification) return Complete(new(request.RequestId, request.ConversationId, intent.ClarificationQuestion!, ValidationStatus.RequiresReview, intent.Confidence, intent, [], [], Metrics(total, intentMs, 0, 0, 0, 0, null, null, 0)));
            var (knowledge, retrievalMs) = await Timed("retrieval", () => retrieval.RetrieveAsync(new(request.Message, intent, request.UserContext, options.Value.MaxResults, options.Value.MaxContextTokens), timeout.Token));
            if (knowledge.Items.Count == 0 && intent.RequiredTools.Count == 0) return Complete(new(request.RequestId, request.ConversationId, "Não encontrei informações suficientes na base de conhecimento para responder com segurança.", ValidationStatus.InsufficientKnowledge, intent.Confidence, intent, [], [], Metrics(total, intentMs, retrievalMs, 0, 0, 0, null, null, 0)));
            CacheScopeFingerprint? fingerprint = null; string? responseKey = null;
            if (advanced.Value.ResponseCacheEnabled && knowledge.AccessScope is not null)
            {
                var retrievalRequest = new RetrievalRequest(request.Message, intent, request.UserContext, options.Value.MaxResults, options.Value.MaxContextTokens);
                fingerprint = cacheKeys.CreateFingerprint(knowledge.AccessScope, retrievalRequest, knowledge.KnowledgeRevision); responseKey = cacheKeys.CreateResponseKey(fingerprint, "ollama-v1");
                var cached = await responseCache.GetAsync(responseKey, fingerprint, timeout.Token);
                if (cached is not null)
                {
                    var value = cached.Response with { RequestId = request.RequestId, ConversationId = request.ConversationId, Metrics = cached.Response.Metrics with { TotalLatencyMs = total.ElapsedMilliseconds, IntentLatencyMs = intentMs, RetrievalLatencyMs = retrievalMs } };
                    return Complete(value);
                }
            }
            var (basePrompt, promptMs) = await Timed("prompt.build", () => prompts.BuildAsync(new(request.Message, intent, knowledge, request.UserContext), timeout.Token));
            var prompt = basePrompt with { Tools = toolCatalog.Enabled };
            var (generated, firstModelMs) = await Timed("llm.chat", () => model.ChatAsync(prompt, timeout.Token));
            var modelMs = firstModelMs;
            var toolCalls = 0; var perTool = new Dictionary<string, int>(StringComparer.Ordinal); var usedTools = false;
            while (generated.ToolCalls.Count > 0)
            {
                var results = new List<ToolExecutionResult>();
                foreach (var call in generated.ToolCalls)
                {
                    if (toolCalls >= toolOptions.Value.MaxCallsPerRequest) throw new AiGatewayException(ErrorCodes.ToolLimitExceeded, "O limite de ferramentas desta solicitação foi atingido.");
                    perTool.TryGetValue(call.Name, out var repeated);
                    if (repeated >= toolOptions.Value.MaxCallsPerTool) throw new AiGatewayException(ErrorCodes.ToolLimitExceeded, "O limite de repetição da ferramenta foi atingido.");
                    toolCalls++; perTool[call.Name] = repeated + 1;
                    var result = await toolExecutor.ExecuteAsync(new(request.RequestId, request.TraceId, request.ConversationId, request.UserContext, call), timeout.Token);
                    if (result.ErrorCode == ToolErrorCodes.NotRegistered) throw new AiGatewayException(ErrorCodes.UnsupportedTool, "A ferramenta solicitada não é permitida.");
                    results.Add(result); usedTools = true;
                }
                prompt = AppendToolResults(prompt, generated, results);
                var (next, nextMs) = await Timed("llm.chat", () => model.ChatAsync(prompt, timeout.Token)); generated = next; modelMs += nextMs;
            }
            var (validation, validationMs) = await Timed("response.validate", () => validator.ValidateAsync(new(generated, prompt), timeout.Token));
            var cited = prompt.Sources.Where(s => validation.CitedSourceIds.Contains(s.Id, StringComparer.OrdinalIgnoreCase)).Select(s => new AiSource(s.Id, s.Type, s.Title, s.Version)).ToArray();
            var response = new AiResponse(request.RequestId, request.ConversationId, validation.Answer, validation.Status, Confidence(intent, knowledge, validation), intent, request.IncludeSources ? cited : [], validation.Reasons, Metrics(total, intentMs, retrievalMs, promptMs, modelMs, validationMs, generated.PromptTokens, generated.CompletionTokens, prompt.EstimatedTokens));
            if (!usedTools && responseKey is not null && fingerprint is not null && cacheAdmission.CanCacheResponse(new(validation.Status, sanitizer.Sanitize(validation.Answer) != validation.Answer, generated.HasToolCalls, false, validation.CitedSourceIds, knowledge.Items.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase))))
            {
                var now = DateTimeOffset.UtcNow; var ttl = TimeSpan.FromMinutes(advanced.Value.ResponseCacheTtlMinutes); var metadata = new CacheEntryMetadata(fingerprint.Scope, fingerprint.Query, fingerprint.SchemaVersion, fingerprint.PolicyVersion, knowledge.KnowledgeRevision, request.UserContext.ErpVersion, fingerprint.Permission, now, now + ttl);
                await responseCache.SetAsync(responseKey, new(metadata, response, validation.CitedSourceIds), ttl, timeout.Token);
            }
            return Complete(response);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested) { telemetry.RecordError(ErrorCodes.Timeout); throw new AiGatewayException(ErrorCodes.Timeout, "A solicitação excedeu o tempo limite.", ex); }
        catch (AiGatewayException ex) { telemetry.RecordError(ex.Code); throw; }
    }
    private async Task<(T, long)> Timed<T>(string stage, Func<Task<T>> action) { using var scope = telemetry.StartStage(stage); var watch = Stopwatch.StartNew(); var result = await action(); return (result, watch.ElapsedMilliseconds); }
    private AiResponse Complete(AiResponse response) { telemetry.RecordCompleted(response); return response; }
    private static double Confidence(IntentResult intent, RetrievalResult retrieval, ResponseValidationResult validation) => Math.Clamp(intent.Confidence * .3 + (retrieval.Items.Count > 0 ? .35 : 0) + (validation.Status == ValidationStatus.Grounded ? .35 : 0), 0, 1);
    private static AiMetrics Metrics(Stopwatch total, long intent, long retrieval, long prompt, long model, long validation, int? pt, int? ct, int context) => new(total.ElapsedMilliseconds, intent, retrieval, prompt, model, validation, pt, ct, context);
    private static PromptPackage AppendToolResults(PromptPackage prompt, ModelResponse generated, IReadOnlyList<ToolExecutionResult> results)
    {
        var messages = prompt.Messages.ToList();
        if (!string.IsNullOrWhiteSpace(generated.Content)) messages.Add(new("assistant", generated.Content));
        var sources = prompt.Sources.ToList();
        foreach (var result in results)
        {
            var sourceId = $"tool:{result.CallId}";
            var envelope = JsonSerializer.Serialize(new { sourceId, result.ToolName, result.Success, data = result.Data, errorCode = result.ErrorCode, message = result.SafeMessage });
            messages.Add(new("tool", $"[TOOL RESULT - UNTRUSTED DATA]\n{envelope}\nAo usar fatos deste resultado, cite [{sourceId}]."));
            if (result.Success && result.Data is not null)
                sources.Add(new(sourceId, "tool-result", result.ToolName, result.Data.Value.GetRawText(), null, null, null, 0, 0, 1, true, new Dictionary<string, string> { ["tool"] = result.ToolName }));
        }
        return prompt with { Messages = messages, Sources = sources };
    }
}
