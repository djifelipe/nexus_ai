using System.Text.Json;
using AiGateway.Application;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AiGateway.Infrastructure.Mcp;

public interface IErpMcpTransport { Task<JsonElement> InvokeAsync(string operation, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken); }
public interface IWorkflowMcpTransport { Task<JsonElement> InvokeAsync(string operation, IReadOnlyDictionary<string, object?> arguments, CancellationToken cancellationToken); }

public sealed class ErpMcpTransport(IOptions<ErpMcpOptions> options, ILogger<ErpMcpTransport> logger) : IErpMcpTransport, IAsyncDisposable
{
    private readonly Lazy<Task<McpClient>> _client = new(() => McpTransportFactory.CreateAsync(options.Value.ServerName, options.Value.Transport, options.Value.Endpoint, options.Value.Command, options.Value.Arguments, options.Value.CredentialEnvironmentVariable, options.Value.TimeoutSeconds));
    public Task<JsonElement> InvokeAsync(string operation, IReadOnlyDictionary<string, object?> arguments, CancellationToken ct) => McpTransportFactory.InvokeAsync(_client, operation, arguments, options.Value.TimeoutSeconds, logger, ct);
    public async ValueTask DisposeAsync() { if (_client.IsValueCreated && _client.Value.IsCompletedSuccessfully) await _client.Value.Result.DisposeAsync(); }
}

public sealed class WorkflowMcpTransport(IOptions<KnowledgeBaseMcpOptions> connection, ILogger<WorkflowMcpTransport> logger) : IWorkflowMcpTransport, IAsyncDisposable
{
    private readonly Lazy<Task<McpClient>> _client = new(() => McpTransportFactory.CreateAsync(connection.Value.ServerName, connection.Value.Transport, connection.Value.Endpoint, connection.Value.Command, connection.Value.Arguments, connection.Value.CredentialEnvironmentVariable, connection.Value.TimeoutSeconds));
    public Task<JsonElement> InvokeAsync(string operation, IReadOnlyDictionary<string, object?> arguments, CancellationToken ct) => McpTransportFactory.InvokeAsync(_client, operation, arguments, connection.Value.TimeoutSeconds, logger, ct);
    public async ValueTask DisposeAsync() { if (_client.IsValueCreated && _client.Value.IsCompletedSuccessfully) await _client.Value.Result.DisposeAsync(); }
}

internal static class McpTransportFactory
{
    internal static Task<McpClient> CreateAsync(string name, string transportName, string? endpoint, string command, string[] arguments, string credentialVariable, int timeoutSeconds)
    {
        IClientTransport transport;
        if (transportName.Equals("Stdio", StringComparison.OrdinalIgnoreCase))
        {
            var environment = StdioClientTransportOptions.GetDefaultEnvironmentVariables();
            var credential = Environment.GetEnvironmentVariable(credentialVariable);
            if (!string.IsNullOrWhiteSpace(credential)) environment[credentialVariable] = credential;
            transport = new StdioClientTransport(new StdioClientTransportOptions { Name = name, Command = command, Arguments = arguments, InheritEnvironmentVariables = false, EnvironmentVariables = environment, ShutdownTimeout = TimeSpan.FromSeconds(2) });
        }
        else transport = new HttpClientTransport(new HttpClientTransportOptions { Name = name, Endpoint = new Uri(endpoint ?? throw new InvalidOperationException($"Endpoint MCP ausente para {name}.")), TransportMode = HttpTransportMode.StreamableHttp, ConnectionTimeout = TimeSpan.FromSeconds(timeoutSeconds) });
        return McpClient.CreateAsync(transport);
    }

    internal static async Task<JsonElement> InvokeAsync<TLogger>(Lazy<Task<McpClient>> client, string operation, IReadOnlyDictionary<string, object?> arguments, int timeoutSeconds, ILogger<TLogger> logger, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            var result = await (await client.Value).CallToolAsync(operation, arguments, cancellationToken: timeout.Token);
            var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "{}";
            using var envelope = JsonDocument.Parse(text);
            var root = envelope.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("result", out var nested))
            {
                if (nested.ValueKind == JsonValueKind.String)
                {
                    using var inner = JsonDocument.Parse(nested.GetString() ?? "{}");
                    return FirstObject(inner.RootElement);
                }
                return FirstObject(nested);
            }
            return FirstObject(root);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (JsonException ex) { throw new Domain.Tools.ToolResultRejectedException(ex.Message); }
        catch (Exception ex)
        {
            logger.LogWarning("Read-only MCP operation {Operation} failed with type {ExceptionType}", operation, ex.GetType().Name);
            throw new Domain.Tools.ToolDependencyException("MCP indisponível.", ex);
        }
    }

    private static JsonElement FirstObject(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array) return value.GetArrayLength() == 0 ? JsonSerializer.SerializeToElement<object?>(null) : value[0].Clone();
        return value.Clone();
    }
}

