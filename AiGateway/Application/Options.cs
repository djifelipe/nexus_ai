using System.ComponentModel.DataAnnotations;

namespace AiGateway.Application;

public sealed class AiGatewayOptions
{
    public const string SectionName = "AiGateway";
    public bool Enabled { get; init; } = true;
    [Range(1, 100)] public int MaxResults { get; init; } = 15;
    [Range(512, 100_000)] public int MaxContextTokens { get; init; } = 8000;
    [Range(128, 50_000)] public int ResponseTokenReserve { get; init; } = 5000;
    [Range(1000, 200_000)] public int ModelTokenLimit { get; init; } = 16000;
    [Range(0, 1)] public double UnknownThreshold { get; init; } = 0.55;
    [Range(0, 1)] public double MultiModuleThreshold { get; init; } = 0.75;
    [Range(1, 300)] public int TotalTimeoutSeconds { get; init; } = 10;
}

public sealed class AdvancedRetrievalOptions
{
    public const string SectionName = "AdvancedRetrieval";
    public bool AdvancedRankingEnabled { get; init; }
    public bool GraphEnabled { get; init; }
    public bool SearchCacheEnabled { get; init; }
    public bool ResponseCacheEnabled { get; init; }
    public bool ShadowModeEnabled { get; init; }
    [Range(1, 4)] public int GraphDepth { get; init; } = 2;
    [Range(1, 1000)] public int GraphMaxNodes { get; init; } = 100;
    [Range(1, 500)] public int GraphMaxPaths { get; init; } = 50;
    [Range(0, 1)] public double SemanticDeduplicationThreshold { get; init; } = .92;
    [Range(1, 15)] public int MaxResults { get; init; } = 15;
    [Range(512, 8000)] public int MaxContextTokens { get; init; } = 8000;
    [Range(100, 800)] public int RetrievalTimeoutMs { get; init; } = 800;
    [Range(50, 600)] public int SourceTimeoutMs { get; init; } = 450;
    [Range(25, 400)] public int GraphTimeoutMs { get; init; } = 200;
    [Range(25, 250)] public int ProcessingTimeoutMs { get; init; } = 100;
    [Range(1, 60)] public int SearchCacheTtlMinutes { get; init; } = 5;
    [Range(1, 30)] public int ResponseCacheTtlMinutes { get; init; } = 2;
    [Required] public string SchemaVersion { get; init; } = "2";
    [Required] public string RankingPolicyVersion { get; init; } = "phase-2-v1";
    [Required] public string CacheKeySecret { get; init; } = "development-only-change-me";
    public string[] AllowedGraphRelations { get; init; } = ["HAS_WORKFLOW", "REQUIRES_PERMISSION", "HAS_RULE", "EMITS_EVENT", "USES_ENTITY", "HAS_EXCEPTION"];
    public RetrievalWeightOptions HowTo { get; init; } = new(.45, .35, .20);
    public RetrievalWeightOptions Explanation { get; init; } = new(.20, .50, .30);
    public RetrievalWeightOptions PermissionCheck { get; init; } = new(.60, .05, .35);
    public RetrievalWeightOptions ImpactAnalysis { get; init; } = new(.25, .20, .55);
    public RetrievalWeightOptions Default { get; init; } = new(.35, .45, .20);
}

public sealed record RetrievalWeightOptions(double Sql, double Vector, double Graph);

public sealed class RetrievalCacheOptions
{
    public const string SectionName = "RetrievalCache";
    public string? RedisConfiguration { get; init; }
    public string InstanceName { get; init; } = "ai-gateway:";
}

public sealed class KnowledgeBaseMcpOptions
{
    public const string SectionName = "Mcp:KnowledgeBase";
    [Required] public string ServerName { get; init; } = "supabase-mcp-server_kb";
    [Required] public string Transport { get; init; } = "Stdio";
    public string? Endpoint { get; init; }
    public string Command { get; init; } = "npx";
    public string[] Arguments { get; init; } = [];
    public string CredentialEnvironmentVariable { get; init; } = "SUPABASE_ACCESS_TOKEN";
    [Range(1, 60)] public int TimeoutSeconds { get; init; } = 5;
    [Required] public string QueryTool { get; init; } = "execute_sql";
}

public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";
    [Required, Url] public string Endpoint { get; init; } = "http://localhost:11434";
    [Required] public string ChatModel { get; init; } = "qwen2.5:7b";
    [Required] public string EmbeddingModel { get; init; } = "nomic-embed-text";
    [Range(1, 2_000)] public int EmbeddingDimensions { get; init; } = 768;
    public bool Think { get; init; } = false;
    [Range(32, 4_096)] public int MaxOutputTokens { get; init; } = 256;
    [Range(1, 300)] public int TimeoutSeconds { get; init; } = 8;
}

public sealed class ReadOnlyToolsOptions
{
    public const string SectionName = "ReadOnlyTools";
    [Range(1, 5)] public int MaxCallsPerRequest { get; init; } = 5;
    [Range(1, 2)] public int MaxCallsPerTool { get; init; } = 2;
    [Range(1, 60)] public int TimeoutSeconds { get; init; } = 10;
    public string[] Enabled { get; init; } = [];
    public string[] CustomerSummaryAllowedFields { get; init; } = ["displayName", "status", "city", "state"];
}

public sealed class AdvancedValidationOptions
{
    public const string SectionName = "AdvancedValidation";
    public bool Enabled { get; init; }
    public bool ShadowModeEnabled { get; init; }
    public bool RegenerationEnabled { get; init; }
    public bool ModelClaimExtractionEnabled { get; init; }
    [Range(100, 100_000)] public int MaxResponseCharacters { get; init; } = 20_000;
    [Range(1, 100)] public int MaxClaims { get; init; } = 30;
    [Range(1, 20)] public int MaxEvidenceCandidatesPerClaim { get; init; } = 5;
    [Range(50, 10_000)] public int ExternalTimeoutMs { get; init; } = 1_500;
    [Range(0, 1)] public double RetrievalWeight { get; init; } = .35;
    [Range(0, 1)] public double CitationWeight { get; init; } = .25;
    [Range(0, 1)] public double SemanticWeight { get; init; } = .25;
    [Range(0, 1)] public double IntentWeight { get; init; } = .15;
    [Range(0, 1)] public double GroundedThreshold { get; init; } = .80;
    [Range(0, 1)] public double PartiallyGroundedThreshold { get; init; } = .55;
    [Range(0, 1)] public double SemanticSupportThreshold { get; init; } = .55;
    [Range(0, 1)] public double SemanticContradictionThreshold { get; init; } = .25;
    [Required] public string PolicyVersion { get; init; } = "phase-4-v1";
}

public sealed class ErpMcpOptions
{
    public const string SectionName = "Mcp:Erp";
    [Required] public string ServerName { get; init; } = "supabase-mcp-server_ts";
    [Required] public string Transport { get; init; } = "Stdio";
    public string? Endpoint { get; init; }
    public string Command { get; init; } = "npx";
    public string[] Arguments { get; init; } = [];
    public string CredentialEnvironmentVariable { get; init; } = "SUPABASE_ACCESS_TOKEN_TS";
    [Range(1, 60)] public int TimeoutSeconds { get; init; } = 10;
    public string InventoryOperation { get; init; } = "inventory_get_balance";
    public string InvoiceOperation { get; init; } = "invoice_get_status";
    public string PermissionOperation { get; init; } = "permission_check";
    public string CustomerOperation { get; init; } = "customer_get_summary";
}

public sealed class WorkflowToolMcpOptions
{
    public const string SectionName = "Mcp:WorkflowTools";
    [Required] public string ServerName { get; init; } = "supabase-mcp-server_kb";
    public string Operation { get; init; } = "workflow_get";
}
