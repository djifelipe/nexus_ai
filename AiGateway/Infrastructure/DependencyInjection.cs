using AiGateway.Application;
using AiGateway.Application.IntentRouting;
using AiGateway.Application.Orchestration;
using AiGateway.Application.Prompting;
using AiGateway.Application.Retrieval;
using AiGateway.Application.Validation;
using AiGateway.Application.Tools;
using AiGateway.Domain.Policies;
using AiGateway.Infrastructure.Graph;
using AiGateway.Infrastructure.Mcp;
using AiGateway.Infrastructure.Observability;
using AiGateway.Infrastructure.Ollama;
using AiGateway.Infrastructure.Redis;
using AiGateway.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace AiGateway.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAiGateway(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AiGatewayOptions>().Bind(configuration.GetSection(AiGatewayOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton<IValidateOptions<AdvancedRetrievalOptions>, AdvancedRetrievalOptionsValidator>();
        services.AddOptions<AdvancedRetrievalOptions>().Bind(configuration.GetSection(AdvancedRetrievalOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<RetrievalCacheOptions>().Bind(configuration.GetSection(RetrievalCacheOptions.SectionName));
        services.AddOptions<KnowledgeBaseMcpOptions>().Bind(configuration.GetSection(KnowledgeBaseMcpOptions.SectionName)).ValidateDataAnnotations()
            .Validate(options => options.ServerName == "supabase-mcp-server_kb", "Somente supabase-mcp-server_kb é permitido para conhecimento.").ValidateOnStart();
        services.AddOptions<OllamaOptions>().Bind(configuration.GetSection(OllamaOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ReadOnlyToolsOptions>().Bind(configuration.GetSection(ReadOnlyToolsOptions.SectionName)).ValidateDataAnnotations()
            .Validate(options => options.Enabled.All(AiGateway.Domain.Tools.ReadOnlyToolNames.All.Contains), "A lista contém uma ferramenta não registrada.").ValidateOnStart();
        services.AddSingleton<IValidateOptions<AdvancedValidationOptions>, AdvancedValidationOptionsValidator>();
        services.AddOptions<AdvancedValidationOptions>().Bind(configuration.GetSection(AdvancedValidationOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ErpMcpOptions>().Bind(configuration.GetSection(ErpMcpOptions.SectionName)).ValidateDataAnnotations()
            .Validate(options => options.ServerName == "supabase-mcp-server_ts", "Somente supabase-mcp-server_ts é permitido para dados do ERP.").ValidateOnStart();
        services.AddOptions<WorkflowToolMcpOptions>().Bind(configuration.GetSection(WorkflowToolMcpOptions.SectionName)).ValidateDataAnnotations()
            .Validate(options => options.ServerName == "supabase-mcp-server_kb", "Somente supabase-mcp-server_kb é permitido para workflow.").ValidateOnStart();

        var redis = configuration.GetSection(RetrievalCacheOptions.SectionName).Get<RetrievalCacheOptions>();
        if (string.IsNullOrWhiteSpace(redis?.RedisConfiguration)) services.AddDistributedMemoryCache();
        else services.AddStackExchangeRedisCache(options => { options.Configuration = redis.RedisConfiguration; options.InstanceName = redis.InstanceName; });

        services.AddSingleton<KnowledgeBaseMcpClient>();
        services.AddSingleton<IKnowledgeBaseMcpClient>(sp => sp.GetRequiredService<KnowledgeBaseMcpClient>());
        services.AddSingleton<IIntentCatalog>(sp => sp.GetRequiredService<KnowledgeBaseMcpClient>());
        services.AddSingleton<IKnowledgeRepository>(sp => sp.GetRequiredService<KnowledgeBaseMcpClient>());
        services.AddSingleton<IGraphKnowledgeExpander, McpGraphKnowledgeExpander>();
        services.AddSingleton<IRetrievalCache, DistributedRetrievalCache>();
        services.AddSingleton<IResponseCache, DistributedResponseCache>();
        services.AddSingleton<ICacheKeyFactory, SecureCacheKeyFactory>();
        services.AddSingleton<RetrievalAccessPolicy>();
        services.AddSingleton<IRetrievalAccessScopeFactory>(sp => sp.GetRequiredService<RetrievalAccessPolicy>());
        services.AddSingleton<IRetrievalAccessPolicy>(sp => sp.GetRequiredService<RetrievalAccessPolicy>());
        services.AddSingleton<IRetrievalRankingPolicy, RetrievalRankingPolicy>();
        services.AddSingleton<IRelationAllowlistPolicy, RelationAllowlistPolicy>();
        services.AddSingleton<ICacheAdmissionPolicy, ResponseCacheAdmissionPolicy>();
        services.AddSingleton<IRetrievalDeduplicationPolicy, RetrievalDeduplicationPolicy>();
        services.AddSingleton<ScoreFusionService>();
        services.AddSingleton<IToolCatalog, ReadOnlyToolCatalog>();
        services.AddSingleton<IErpMcpTransport, ErpMcpTransport>();
        services.AddSingleton<IWorkflowMcpTransport, WorkflowMcpTransport>();
        services.AddSingleton<ErpMcpReadAdapter>();
        services.AddSingleton<IErpReadPort>(sp => sp.GetRequiredService<ErpMcpReadAdapter>());
        services.AddSingleton<IErpMcpClient>(sp => sp.GetRequiredService<ErpMcpReadAdapter>());
        services.AddSingleton<IWorkflowReadPort, WorkflowMcpReadAdapter>();
        services.AddSingleton<IToolHandler, InventoryBalanceToolHandler>();
        services.AddSingleton<IToolHandler, InvoiceStatusToolHandler>();
        services.AddSingleton<IToolHandler, PermissionCheckToolHandler>();
        services.AddSingleton<IToolHandler, WorkflowToolHandler>();
        services.AddSingleton<IToolHandler, CustomerSummaryToolHandler>();
        services.AddSingleton<IToolExecutor, ReadOnlyToolExecutor>();

        services.AddHttpClient<OllamaClient>((sp, client) => { var value = sp.GetRequiredService<IOptions<OllamaOptions>>().Value; client.BaseAddress = new Uri(value.Endpoint.TrimEnd('/') + "/"); client.Timeout = Timeout.InfiniteTimeSpan; });
        services.AddHttpClient<OllamaHealthCheck>((sp, client) => { var value = sp.GetRequiredService<IOptions<OllamaOptions>>().Value; client.BaseAddress = new Uri(value.Endpoint.TrimEnd('/') + "/"); client.Timeout = TimeSpan.FromSeconds(Math.Min(3, value.TimeoutSeconds)); });
        services.AddTransient<ILanguageModelClient>(sp => sp.GetRequiredService<OllamaClient>());
        services.AddTransient<IEmbeddingClient>(sp => sp.GetRequiredService<OllamaClient>());
        services.AddSingleton<ITokenEstimator, ApproximateTokenEstimator>();
        services.AddSingleton<ISensitiveDataSanitizer, SensitiveDataSanitizer>();
        services.AddSingleton<ISensitiveDataDetector, SensitiveDataDetector>();
        services.AddSingleton<IAiTelemetry, AiTelemetry>();
        services.AddScoped<IIntentRouter, RuleBasedIntentRouter>();
        services.AddScoped<IKnowledgeRetriever, HybridKnowledgeRetriever>();
        services.AddScoped<IPromptBuilder, GroundedPromptBuilder>();
        services.AddScoped<CitationResponseValidator>();
        services.AddScoped<DeterministicClaimExtractor>();
        services.AddScoped<IModelClaimExtractor, ModelClaimExtractor>();
        services.AddScoped<IClaimExtractor, HybridClaimExtractor>();
        services.AddScoped<AiGateway.Domain.Policies.AdvancedValidationPolicy>(sp => { var value = sp.GetRequiredService<IOptions<AdvancedValidationOptions>>().Value; return new(value.RetrievalWeight, value.CitationWeight, value.SemanticWeight, value.IntentWeight, value.GroundedThreshold, value.PartiallyGroundedThreshold, value.SemanticSupportThreshold, value.SemanticContradictionThreshold, value.MaxResponseCharacters, value.MaxClaims, value.MaxEvidenceCandidatesPerClaim, value.ExternalTimeoutMs, value.PolicyVersion); });
        services.AddScoped<ISemanticGroundingEvaluator, LexicalSemanticGroundingEvaluator>();
        services.AddScoped<IResponseValidator, AdvancedResponseValidator>();
        services.AddScoped<IAiOrchestrator, AiOrchestrator>();
        services.AddHealthChecks().AddCheck<KnowledgeBaseMcpHealthCheck>("mcp-kb").AddCheck<OllamaHealthCheck>("ollama");
        return services;
    }
}
