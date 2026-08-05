using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using AiGateway.Application;
using AiGateway.Domain;
using Microsoft.Extensions.Options;

namespace AiGateway.Infrastructure.Ollama;

public sealed class OllamaClient(HttpClient httpClient, IOptions<OllamaOptions> options) : ILanguageModelClient, IEmbeddingClient
{
    public int Dimensions => options.Value.EmbeddingDimensions;
    public async Task<ModelResponse> ChatAsync(PromptPackage prompt, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            var watch = Stopwatch.StartNew();
            var tools = prompt.Tools.Select(x => new { type = "function", function = new { name = x.Name, description = x.Description, parameters = x.InputSchema.RootElement } }).ToArray();
            using var response = await httpClient.PostAsJsonAsync("api/chat", new { model = options.Value.ChatModel, stream = false, think = options.Value.Think, messages = prompt.Messages.Select(x => new { role = x.Role, content = x.Content }), tools, options = new { num_predict = options.Value.MaxOutputTokens } }, timeout.Token);
            response.EnsureSuccessStatusCode();
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
            var root = document.RootElement;
            if (!root.TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.String) throw new AiGatewayException(ErrorCodes.OllamaInvalidResponse, "O modelo retornou uma resposta inválida.");
            var toolCalls = ParseToolCalls(message);
            return new(content.GetString()!, GetInt(root, "prompt_eval_count"), GetInt(root, "eval_count"), root.TryGetProperty("done_reason", out var reason) ? reason.GetString() : null, toolCalls.Length > 0, watch.Elapsed.TotalMilliseconds) { ToolCalls = toolCalls };
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested) { throw new AiGatewayException(ErrorCodes.Timeout, "O modelo excedeu o tempo limite.", ex); }
        catch (AiGatewayException) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or JsonException) { throw new AiGatewayException(ErrorCodes.OllamaUnavailable, "O serviço de linguagem está temporariamente indisponível.", ex); }
    }
    public async Task<ReadOnlyMemory<float>> CreateAsync(string input, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("api/embed", new { model = options.Value.EmbeddingModel, input }, cancellationToken); response.EnsureSuccessStatusCode();
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var embeddings = document.RootElement.GetProperty("embeddings"); if (embeddings.GetArrayLength() == 0) throw new JsonException("Embedding ausente.");
            var values = embeddings[0].EnumerateArray().Select(x => x.GetSingle()).ToArray();
            if (values.Length != Dimensions) throw new AiGatewayException(ErrorCodes.EmbeddingUnavailable, "A dimensão do embedding é incompatível com a configuração.");
            return values;
        }
        catch (AiGatewayException) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or JsonException) { throw new AiGatewayException(ErrorCodes.EmbeddingUnavailable, "O serviço de embeddings está temporariamente indisponível.", ex); }
    }
    private static int? GetInt(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : null;
    private static Domain.Tools.ToolCall[] ParseToolCalls(JsonElement message)
    {
        if (!message.TryGetProperty("tool_calls", out var calls) || calls.ValueKind != JsonValueKind.Array) return [];
        var results = new List<Domain.Tools.ToolCall>(); var index = 0;
        foreach (var call in calls.EnumerateArray())
        {
            if (!call.TryGetProperty("function", out var function) || !function.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String) continue;
            var id = call.TryGetProperty("id", out var idValue) && idValue.ValueKind == JsonValueKind.String ? idValue.GetString()! : $"tool-{index}";
            JsonElement arguments = JsonSerializer.SerializeToElement(new { });
            if (function.TryGetProperty("arguments", out var args))
            {
                if (args.ValueKind == JsonValueKind.Object) arguments = args.Clone();
                else if (args.ValueKind == JsonValueKind.String) try { using var parsed = JsonDocument.Parse(args.GetString() ?? "{}"); arguments = parsed.RootElement.Clone(); } catch (JsonException) { arguments = JsonSerializer.SerializeToElement(args.GetString()); }
            }
            results.Add(new(id, name.GetString()!, arguments)); index++;
        }
        return results.ToArray();
    }
}
