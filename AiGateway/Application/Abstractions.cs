using AiGateway.Domain;
using AiGateway.Domain.Tools;
using System.Text.Json;

namespace AiGateway.Application;

public interface IAiOrchestrator { Task<AiResponse> ExecuteAsync(AiRequest request, CancellationToken cancellationToken); }
public interface IIntentRouter { Task<IntentResult> RouteAsync(IntentRouterRequest request, CancellationToken cancellationToken); }
public interface IKnowledgeRetriever { Task<RetrievalResult> RetrieveAsync(RetrievalRequest request, CancellationToken cancellationToken); }
public interface IPromptBuilder { Task<PromptPackage> BuildAsync(PromptBuildRequest request, CancellationToken cancellationToken); }
public interface ILanguageModelClient { Task<ModelResponse> ChatAsync(PromptPackage prompt, CancellationToken cancellationToken); }
public interface IResponseValidator { Task<ResponseValidationResult> ValidateAsync(ResponseValidationRequest request, CancellationToken cancellationToken); }
public interface IKnowledgeBaseMcpClient : IIntentCatalog, IKnowledgeRepository
{
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken);
}
public interface IErpMcpClient { }
public interface IIntentCatalog { Task<IReadOnlyList<IntentCatalogEntry>> GetActiveAsync(string companyId, IReadOnlySet<string> permissions, CancellationToken cancellationToken); }
public interface IKnowledgeRepository
{
    Task<IReadOnlyList<KnowledgeItem>> SearchStructuredAsync(RetrievalRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<KnowledgeItem>> SearchVectorAsync(RetrievalRequest request, ReadOnlyMemory<float> embedding, CancellationToken cancellationToken);
    Task<string> GetKnowledgeRevisionAsync(RetrievalAccessScope scope, CancellationToken cancellationToken) => Task.FromResult("unknown");
    Task<GraphExpansionResult> ExpandGraphAsync(GraphExpansionRequest request, CancellationToken cancellationToken) => Task.FromResult(new GraphExpansionResult([], [], 0, 0, true, "not-supported"));
}
public interface IEmbeddingClient { int Dimensions { get; } Task<ReadOnlyMemory<float>> CreateAsync(string input, CancellationToken cancellationToken); }
public interface ITokenEstimator { int Estimate(string text); }
public interface ISensitiveDataSanitizer { string Sanitize(string input); }
public interface IToolCatalog
{
    IReadOnlyList<ToolDefinition> Enabled { get; }
    bool TryGet(string name, out ToolDefinition definition);
}
public interface IToolExecutor { Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken); }
public interface IToolHandler
{
    string Name { get; }
    Task<JsonElement> ExecuteAsync(UserContext userContext, JsonElement arguments, CancellationToken cancellationToken);
}
public interface IErpReadPort
{
    Task<InventoryBalanceResult?> GetInventoryBalanceAsync(InventoryBalanceQuery query, CancellationToken cancellationToken);
    Task<InvoiceStatusResult?> GetInvoiceStatusAsync(InvoiceStatusQuery query, CancellationToken cancellationToken);
    Task<PermissionResult> CheckPermissionAsync(PermissionQuery query, CancellationToken cancellationToken);
    Task<CustomerSummaryResult?> GetCustomerSummaryAsync(CustomerSummaryQuery query, CancellationToken cancellationToken);
}
public interface IWorkflowReadPort { Task<WorkflowResult?> GetWorkflowAsync(WorkflowQuery query, CancellationToken cancellationToken); }
public interface IAiTelemetry
{
    IDisposable StartRequest(AiRequest request);
    IDisposable StartStage(string stage);
    void RecordCompleted(AiResponse response);
    void RecordError(string code);
    IDisposable StartRetrievalStage(string stage, IReadOnlyDictionary<string, object?>? tags = null);
    void RecordRetrievalEvent(string operation, string outcome, double durationMs, int count = 0);
    IDisposable StartTool(ToolExecutionRequest request, ToolDefinition? definition);
    void RecordTool(ToolExecutionRequest request, ToolExecutionResult result, ToolRiskLevel riskLevel);
}

public interface IRetrievalAccessScopeFactory
{
    RetrievalAccessScope Create(RetrievalRequest request, string? requestId = null, string? traceId = null);
}

public sealed record GraphExpansionRequest(
    IReadOnlyList<GraphSeed> Seeds,
    RetrievalAccessScope AccessScope,
    IReadOnlySet<string> AllowedRelations,
    int MaxDepth,
    int MaxNodes,
    int MaxPaths);

public sealed record GraphExpansionResult(
    IReadOnlyList<KnowledgeItem> Items,
    IReadOnlyList<GraphPath> Paths,
    int VisitedNodes,
    int MaximumDepth,
    bool FiltersVerified,
    string? DegradedReason = null);

public interface IGraphKnowledgeExpander
{
    Task<GraphExpansionResult> ExpandAsync(GraphExpansionRequest request, CancellationToken cancellationToken);
}

public interface IRetrievalCache
{
    Task<RetrievalCacheEntry?> GetAsync(string key, CacheScopeFingerprint expected, CancellationToken cancellationToken);
    Task SetAsync(string key, RetrievalCacheEntry entry, TimeSpan ttl, CancellationToken cancellationToken);
    Task RemoveAsync(string key, CancellationToken cancellationToken);
}

public interface IResponseCache
{
    Task<ResponseCacheEntry?> GetAsync(string key, CacheScopeFingerprint expected, CancellationToken cancellationToken);
    Task SetAsync(string key, ResponseCacheEntry entry, TimeSpan ttl, CancellationToken cancellationToken);
    Task RemoveAsync(string key, CancellationToken cancellationToken);
}

public interface ICacheKeyFactory
{
    CacheScopeFingerprint CreateFingerprint(RetrievalAccessScope scope, RetrievalRequest request, string knowledgeRevision);
    string CreateSearchKey(CacheScopeFingerprint fingerprint);
    string CreateResponseKey(CacheScopeFingerprint fingerprint, string modelPolicyVersion);
}
