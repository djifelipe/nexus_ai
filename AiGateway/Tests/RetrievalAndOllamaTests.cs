using System.Net;
using System.Text;
using AiGateway.Application;
using AiGateway.Application.Retrieval;
using AiGateway.Domain;
using AiGateway.Infrastructure.Ollama;
using Microsoft.Extensions.Options;

namespace AiGateway.Tests;

public sealed class RetrievalAndOllamaTests
{
    [Fact]
    public async Task Retriever_deduplicates_orders_and_enforces_result_budget()
    {
        var items = Enumerable.Range(1, 20).Select(i => Source($"s{i}", i == 1, 1 - (i / 100d))).ToArray(); var retriever = new HybridKnowledgeRetriever(new FakeRepository(items), new FakeEmbedding(), new Approximate(), Options.Create(new AiGatewayOptions { MaxResults = 15, MaxContextTokens = 8000 }));
        var result = await retriever.RetrieveAsync(new("question", Intent(), User(), 15, 8000), default);
        Assert.Equal(15, result.Items.Count); Assert.Equal("s1", result.Items[0].Id); Assert.True(result.Diagnostics.ResultLimitApplied); Assert.Contains("tenant", result.Diagnostics.AppliedFilters);
    }

    [Fact]
    public async Task Ollama_maps_content_tokens_and_tool_calls()
    {
        var json = """{"message":{"content":"Resposta [s1]","tool_calls":[{"function":{"name":"invoice.cancel"}}]},"prompt_eval_count":12,"eval_count":4,"done_reason":"stop"}""";
        var client = Client(new StubHandler(HttpStatusCode.OK, json)); var response = await client.ChatAsync(new([], [], 0, "q"), default);
        Assert.Equal("Resposta [s1]", response.Content); Assert.Equal(12, response.PromptTokens); Assert.Equal(4, response.CompletionTokens); Assert.True(response.HasToolCalls);
        Assert.Equal("invoice.cancel", Assert.Single(response.ToolCalls).Name);
    }

    [Fact]
    public async Task Ollama_rejects_malformed_response()
    {
        var client = Client(new StubHandler(HttpStatusCode.OK, "{}")); var error = await Assert.ThrowsAsync<AiGatewayException>(() => client.ChatAsync(new([], [], 0, "q"), default)); Assert.Equal(ErrorCodes.OllamaInvalidResponse, error.Code);
    }

    [Fact]
    public async Task Ollama_maps_timeout_to_stable_error()
    {
        var client = Client(new DelayedHandler()); var error = await Assert.ThrowsAsync<AiGatewayException>(() => client.ChatAsync(new([], [], 0, "q"), default)); Assert.Equal(ErrorCodes.Timeout, error.Code);
    }

    private static OllamaClient Client(HttpMessageHandler handler) => new(new HttpClient(handler) { BaseAddress = new Uri("http://ollama/") }, Options.Create(new OllamaOptions { Endpoint = "http://ollama", ChatModel = "test", EmbeddingModel = "embed", EmbeddingDimensions = 3, TimeoutSeconds = 1 }));
    private static UserContext User() => new("company", "user", "1", "pt-BR", new HashSet<string> { "p" }, new("Fiscal", null, null));
    private static IntentResult Intent() => new("Fiscal", "NFe", "Cancel", "Entity", IntentType.HowTo, .9, [], [], false, null, "test", ["Fiscal"]);
    private static KnowledgeItem Source(string id, bool critical, double score) => new(id, "workflow", id, new string('x', 40), "Fiscal", "NFe", "1", score, score, score, critical, new Dictionary<string, string>());
    private sealed class FakeEmbedding : IEmbeddingClient { public int Dimensions => 3; public Task<ReadOnlyMemory<float>> CreateAsync(string input, CancellationToken cancellationToken) => Task.FromResult<ReadOnlyMemory<float>>(new float[] { 1, 2, 3 }); }
    private sealed class Approximate : ITokenEstimator { public int Estimate(string text) => Math.Max(1, text.Length / 4); }
    private sealed class FakeRepository(IReadOnlyList<KnowledgeItem> items) : IKnowledgeRepository
    { public Task<IReadOnlyList<KnowledgeItem>> SearchStructuredAsync(RetrievalRequest request, CancellationToken cancellationToken) => Task.FromResult(items); public Task<IReadOnlyList<KnowledgeItem>> SearchVectorAsync(RetrievalRequest request, ReadOnlyMemory<float> embedding, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<KnowledgeItem>>(items.Take(5).ToArray()); }
    private sealed class StubHandler(HttpStatusCode status, string json) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") }); }
    private sealed class DelayedHandler : HttpMessageHandler
    { protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken); return new(HttpStatusCode.OK); } }
}
