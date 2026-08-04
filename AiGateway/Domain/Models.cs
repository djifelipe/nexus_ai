namespace AiGateway.Domain;

public enum IntentType { HowTo, Explanation, Troubleshooting, DataQuery, Validation, Navigation, PermissionCheck, ImpactAnalysis, Comparison, Unknown }
public enum ValidationStatus { Grounded, PartiallyGrounded, InsufficientKnowledge, Unsafe, InvalidFormat, RequiresReview }

public sealed record ScreenContext(string? CurrentModule, string? CurrentScreen, string? SelectedEntityId);
public sealed record UserContext(string CompanyId, string UserId, string ErpVersion, string Language, IReadOnlySet<string> Permissions, ScreenContext Screen);

public sealed record IntentResult(
    string? Module,
    string? Feature,
    string? Action,
    string? Entity,
    IntentType Type,
    double Confidence,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> RequiredTools,
    bool RequiresClarification,
    string? ClarificationQuestion,
    string Strategy,
    IReadOnlyList<string> CandidateModules);

public sealed record IntentCatalogEntry(
    string Module,
    string? Feature,
    string? Action,
    string? Entity,
    IntentType Type,
    IReadOnlyList<string> Terms,
    double Weight,
    string? RequiredPermission);

public sealed record KnowledgeItem(
    string Id,
    string Type,
    string Title,
    string Content,
    string? Module,
    string? Feature,
    string? Version,
    double VectorScore,
    double SqlScore,
    double FinalScore,
    bool IsCritical,
    IReadOnlyDictionary<string, string> Metadata)
{
    public double GraphScore { get; init; }
    public IReadOnlyList<ScoreContribution> ScoreContributions { get; init; } = [];
    public IReadOnlyList<GraphPath> GraphPaths { get; init; } = [];
    public IReadOnlyList<SuppressedSource> SuppressedSources { get; init; } = [];
    public string RankingPolicyVersion { get; init; } = "phase-1";
}

public sealed record RetrievalDiagnostics(IReadOnlyList<string> Strategies, IReadOnlyList<string> AppliedFilters, int CandidateCount, bool ResultLimitApplied, bool TokenLimitApplied)
{
    public AdvancedRetrievalDiagnostics Advanced { get; init; } = AdvancedRetrievalDiagnostics.PhaseOne;
}
public sealed record RetrievalResult(IReadOnlyList<KnowledgeItem> Items, RetrievalDiagnostics Diagnostics)
{
    public string KnowledgeRevision { get; init; } = "unknown";
    public RetrievalAccessScope? AccessScope { get; init; }
}
public sealed record PromptMessage(string Role, string Content);
public sealed record PromptPackage(IReadOnlyList<PromptMessage> Messages, IReadOnlyList<KnowledgeItem> Sources, int EstimatedTokens, string OriginalQuestion);
public sealed record ModelResponse(string Content, int? PromptTokens, int? CompletionTokens, string? FinishReason, bool HasToolCalls, double? FirstTokenLatencyMs);
public sealed record ResponseValidationResult(ValidationStatus Status, string Answer, IReadOnlyList<string> CitedSourceIds, IReadOnlyList<string> Reasons);
public sealed record AiSource(string SourceId, string SourceType, string Title, string? Version);
public sealed record AiMetrics(long TotalLatencyMs, long IntentLatencyMs, long RetrievalLatencyMs, long PromptLatencyMs, long ModelLatencyMs, long ValidationLatencyMs, int? PromptTokens, int? CompletionTokens, int ContextTokens);
public sealed record AiResponse(string RequestId, string? ConversationId, string Answer, ValidationStatus Status, double Confidence, IntentResult Intent, IReadOnlyList<AiSource> Sources, IReadOnlyList<string> Warnings, AiMetrics Metrics);

public sealed record AiRequest(string? ConversationId, string Message, string CompanyId, string UserId, ScreenContext Screen, bool Stream, bool IncludeSources, UserContext UserContext, string RequestId, string TraceId);
public sealed record IntentRouterRequest(string Question, UserContext UserContext);
public sealed record RetrievalRequest(string Question, IntentResult Intent, UserContext UserContext, int MaxResults = 15, int MaxContextTokens = 8000)
{
    public IReadOnlySet<string> AllowedContentTypes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "workflow", "business-rule", "faq", "example", "documentation", "permission", "validation", "exception" };
}
public sealed record PromptBuildRequest(string Question, IntentResult Intent, RetrievalResult Retrieval, UserContext UserContext, string? ConversationSummary = null);
public sealed record ResponseValidationRequest(ModelResponse ModelResponse, PromptPackage Prompt);

public static class ErrorCodes
{
    public const string InvalidInput = "AI_INVALID_INPUT";
    public const string AccessDenied = "AI_ACCESS_DENIED";
    public const string InsufficientKnowledge = "AI_INSUFFICIENT_KNOWLEDGE";
    public const string Cancelled = "AI_CANCELLED";
    public const string Timeout = "AI_TIMEOUT";
    public const string DatabaseUnavailable = "AI_DATABASE_UNAVAILABLE";
    public const string EmbeddingUnavailable = "AI_EMBEDDING_UNAVAILABLE";
    public const string OllamaUnavailable = "AI_OLLAMA_UNAVAILABLE";
    public const string OllamaInvalidResponse = "AI_OLLAMA_INVALID_RESPONSE";
    public const string InvalidCitation = "AI_INVALID_CITATION";
    public const string UnsupportedTool = "AI_TOOL_UNSUPPORTED";
    public const string RetrievalAccessContextInvalid = "AI_RETRIEVAL_ACCESS_CONTEXT_INVALID";
    public const string RetrievalFilterIntegrity = "AI_RETRIEVAL_FILTER_INTEGRITY";
    public const string GraphUnavailable = "AI_GRAPH_UNAVAILABLE";
}

public sealed class AiGatewayException(string code, string safeMessage, Exception? innerException = null) : Exception(safeMessage, innerException)
{
    public string Code { get; } = code;
    public string SafeMessage { get; } = safeMessage;
}
