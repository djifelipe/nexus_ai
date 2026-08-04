using System.Diagnostics;
using AiGateway.Application;
using AiGateway.Application.Retrieval;
using AiGateway.Domain;
using AiGateway.Infrastructure.Mcp;
using AiGateway.Infrastructure.Ollama;
using AiGateway.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiGateway.Tests;

public sealed class McpKnowledgeIntegrationTests
{
    [Fact]
    public async Task Live_kb_mcp_enforces_scope_and_orders_vector_results()
    {
        if(Environment.GetEnvironmentVariable("RUN_MCP_INTEGRATION")!="1")return;
        var options=Options.Create(new KnowledgeBaseMcpOptions{ServerName="supabase-mcp-server_kb",Transport="Stdio",Command="npx",Arguments=["-y","@supabase/mcp-server-supabase@latest","--project-ref=wmistourguenjavuymaq"],TimeoutSeconds=20});
        await using var kb=new KnowledgeBaseMcpClient(options,NullLogger<KnowledgeBaseMcpClient>.Instance);
        var embeddings=new OllamaClient(new HttpClient{BaseAddress=new Uri("http://localhost:11434/")},Options.Create(new OllamaOptions{Endpoint="http://localhost:11434",ChatModel="qwen3:8b",EmbeddingModel="nomic-embed-text",EmbeddingDimensions=768,TimeoutSeconds=20}));
        var embedding=await embeddings.CreateAsync("Como cancelar uma NF-e?",default);

        var allowed=Request("company-test","1.0",new HashSet<string>{"Fiscal.NFe.Visualizar"});
        var structured=await kb.SearchStructuredAsync(allowed,default);
        var vector=await kb.SearchVectorAsync(allowed,embedding,default);
        Assert.Contains(structured,item=>item.Id=="kb-nfe-cancelamento");
        Assert.Contains(vector,item=>item.Id=="kb-nfe-cancelamento");
        Assert.Equal(vector.OrderByDescending(item=>item.VectorScore).Select(item=>item.Id),vector.Select(item=>item.Id));
        Assert.DoesNotContain(structured,item=>item.Id is "kb-test-unpublished" or "kb-test-expired" or "kb-test-other-tenant");

        Assert.Empty(await kb.SearchStructuredAsync(Request("company-other-missing","1.0",new HashSet<string>{"Fiscal.NFe.Visualizar"}),default));
        Assert.Empty(await kb.SearchStructuredAsync(Request("company-test","other-version",new HashSet<string>{"Fiscal.NFe.Visualizar"}),default));
        Assert.Empty(await kb.SearchStructuredAsync(Request("company-test","1.0",new HashSet<string>()),default));
    }

    [Fact]
    public async Task Retriever_propagates_dependency_failure_without_unfiltered_fallback()
    {
        var retriever=new HybridKnowledgeRetriever(new FailingRepository(),new FastEmbedding(),new TokenEstimator(),Options.Create(new AiGatewayOptions()));
        var error=await Assert.ThrowsAsync<AiGatewayException>(()=>retriever.RetrieveAsync(Request("company-test","1.0",new HashSet<string>{"Fiscal.NFe.Visualizar"}),default));
        Assert.Equal(ErrorCodes.DatabaseUnavailable,error.Code);
    }

    [Fact]
    public async Task In_memory_retrieval_meets_800ms_target()
    {
        var source=new KnowledgeItem("s1","workflow","title","content","Fiscal","NFe","1",.9,.9,.9,true,new Dictionary<string,string>());
        var retriever=new HybridKnowledgeRetriever(new StaticRepository([source]),new FastEmbedding(),new TokenEstimator(),Options.Create(new AiGatewayOptions()));
        var watch=Stopwatch.StartNew();var result=await retriever.RetrieveAsync(Request("company-test","1.0",new HashSet<string>{"Fiscal.NFe.Visualizar"}),default);
        Assert.Single(result.Items);Assert.True(watch.ElapsedMilliseconds<800,$"Retrieval took {watch.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public void Phase_one_does_not_register_erp_mcp_client()
    {
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Mcp:KnowledgeBase:ServerName"]="supabase-mcp-server_kb",["Mcp:KnowledgeBase:Transport"]="Stdio",["Mcp:KnowledgeBase:Command"]="npx",
            ["Ollama:Endpoint"]="http://localhost:11434",["Ollama:ChatModel"]="qwen3:8b",["Ollama:EmbeddingModel"]="nomic-embed-text",["Ollama:EmbeddingDimensions"]="768"
        }).Build();
        using var provider=new ServiceCollection().AddLogging().AddAiGateway(configuration).BuildServiceProvider();
        Assert.Null(provider.GetService<IErpMcpClient>());
    }

    private static RetrievalRequest Request(string company,string version,IReadOnlySet<string> permissions)=>new("Como cancelar uma NF-e?",new("Fiscal","NFe","NFe.Cancelamento","DocumentoFiscal",IntentType.HowTo,.9,[],[],false,null,"test",["Fiscal"]),new(company,"user",version,"pt-BR",permissions,new("Fiscal","NFeList",null)),15,8000);
    private sealed class FastEmbedding:IEmbeddingClient{public int Dimensions=>768;public Task<ReadOnlyMemory<float>> CreateAsync(string input,CancellationToken cancellationToken)=>Task.FromResult<ReadOnlyMemory<float>>(new float[768]);}
    private sealed class TokenEstimator:ITokenEstimator{public int Estimate(string text)=>Math.Max(1,text.Length/4);}
    private sealed class FailingRepository:IKnowledgeRepository
    {public Task<IReadOnlyList<KnowledgeItem>> SearchStructuredAsync(RetrievalRequest request,CancellationToken cancellationToken)=>Task.FromException<IReadOnlyList<KnowledgeItem>>(new AiGatewayException(ErrorCodes.DatabaseUnavailable,"MCP KB indisponível."));public Task<IReadOnlyList<KnowledgeItem>> SearchVectorAsync(RetrievalRequest request,ReadOnlyMemory<float> embedding,CancellationToken cancellationToken)=>Task.FromResult<IReadOnlyList<KnowledgeItem>>([]);}
    private sealed class StaticRepository(IReadOnlyList<KnowledgeItem> items):IKnowledgeRepository
    {public Task<IReadOnlyList<KnowledgeItem>> SearchStructuredAsync(RetrievalRequest request,CancellationToken cancellationToken)=>Task.FromResult(items);public Task<IReadOnlyList<KnowledgeItem>> SearchVectorAsync(RetrievalRequest request,ReadOnlyMemory<float> embedding,CancellationToken cancellationToken)=>Task.FromResult(items);}
}
