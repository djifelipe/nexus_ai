using AiGateway.Domain;

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
}
public interface IEmbeddingClient { int Dimensions { get; } Task<ReadOnlyMemory<float>> CreateAsync(string input, CancellationToken cancellationToken); }
public interface ITokenEstimator { int Estimate(string text); }
public interface ISensitiveDataSanitizer { string Sanitize(string input); }
public interface IAiTelemetry
{
    IDisposable StartRequest(AiRequest request);
    IDisposable StartStage(string stage);
    void RecordCompleted(AiResponse response);
    void RecordError(string code);
}
