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
