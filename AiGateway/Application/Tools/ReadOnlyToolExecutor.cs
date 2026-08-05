using System.Diagnostics;
using System.Text.Json;
using AiGateway.Domain.Tools;
using Microsoft.Extensions.Options;

namespace AiGateway.Application.Tools;

public sealed class ReadOnlyToolExecutor(
    IToolCatalog catalog,
    IEnumerable<IToolHandler> handlers,
    ISensitiveDataSanitizer sanitizer,
    IAiTelemetry telemetry,
    IOptions<ReadOnlyToolsOptions> options) : IToolExecutor
{
    private readonly IReadOnlyDictionary<string, IToolHandler> _handlers = handlers.ToDictionary(x => x.Name, StringComparer.Ordinal);

    public async Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        catalog.TryGet(request.Call.Name, out var definition);
        using var scope = telemetry.StartTool(request, definition);
        ToolExecutionResult result;
        try
        {
            if (definition is null || definition.RiskLevel != ToolRiskLevel.ReadOnly || definition.RequiresConfirmation || !_handlers.TryGetValue(request.Call.Name, out var handler))
                return Record(ToolExecutionResult.Failed(request.Call, ToolErrorCodes.NotRegistered, "Ferramenta não registrada ou não permitida.", watch.ElapsedMilliseconds), request, definition);
            if (!ValidArguments(definition, request.Call.Arguments) || HasIdentityConflict(request))
            {
                var code = HasIdentityConflict(request) ? ToolErrorCodes.AccessDenied : ToolErrorCodes.InvalidArguments;
                var message = code == ToolErrorCodes.AccessDenied ? "O contexto de identidade da ferramenta é inválido." : "Os argumentos da ferramenta são inválidos.";
                return Record(ToolExecutionResult.Failed(request.Call, code, message, watch.ElapsedMilliseconds), request, definition);
            }
            if (definition.RequiredPermissions.Any(x => !request.UserContext.Permissions.Contains(x)))
                return Record(ToolExecutionResult.Failed(request.Call, ToolErrorCodes.AccessDenied, "Acesso negado.", watch.ElapsedMilliseconds), request, definition);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
            var data = await handler.ExecuteAsync(request.UserContext, request.Call.Arguments, timeout.Token);
            var sanitized = SanitizeAndAllowlist(data, definition);
            result = new(request.Call.Id, request.Call.Name, true, sanitized, null, null, watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            result = ToolExecutionResult.Failed(request.Call, ToolErrorCodes.Cancelled, "Execução cancelada.", watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            result = ToolExecutionResult.Failed(request.Call, ToolErrorCodes.Timeout, "A ferramenta excedeu o tempo limite.", watch.ElapsedMilliseconds);
        }
        catch (ToolRecordNotFoundException)
        {
            result = ToolExecutionResult.Failed(request.Call, ToolErrorCodes.NotFound, "Registro não encontrado.", watch.ElapsedMilliseconds);
        }
        catch (ToolResultRejectedException)
        {
            result = ToolExecutionResult.Failed(request.Call, ToolErrorCodes.ResultRejected, "O resultado da ferramenta não pôde ser retornado com segurança.", watch.ElapsedMilliseconds);
        }
        catch (ToolDependencyException)
        {
            result = ToolExecutionResult.Failed(request.Call, ToolErrorCodes.DependencyUnavailable, "A fonte de dados da ferramenta está temporariamente indisponível.", watch.ElapsedMilliseconds);
        }
        catch (Exception)
        {
            result = ToolExecutionResult.Failed(request.Call, ToolErrorCodes.DependencyUnavailable, "A ferramenta está temporariamente indisponível.", watch.ElapsedMilliseconds);
        }
        return Record(result, request, definition);
    }

    private ToolExecutionResult Record(ToolExecutionResult result, ToolExecutionRequest request, ToolDefinition? definition)
    {
        try { telemetry.RecordTool(request, result, definition?.RiskLevel ?? ToolRiskLevel.ReadOnly); } catch { }
        return result;
    }

    private static bool ValidArguments(ToolDefinition definition, JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object) return false;
        var schema = definition.InputSchema.RootElement;
        var allowed = schema.GetProperty("properties").EnumerateObject().Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var required = schema.GetProperty("required").EnumerateArray().Select(x => x.GetString()!).ToArray();
        foreach (var property in arguments.EnumerateObject())
            if (!allowed.Contains(property.Name) || property.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.Value.GetString())) return false;
        return required.All(name => arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()));
    }

    private static bool HasIdentityConflict(ToolExecutionRequest request) =>
        Conflicts(request.Call.Arguments, "companyId", request.UserContext.CompanyId) || Conflicts(request.Call.Arguments, "userId", request.UserContext.UserId);

    private static bool Conflicts(JsonElement arguments, string property, string expected) =>
        arguments.TryGetProperty(property, out var value) && !string.Equals(value.GetString(), expected, StringComparison.Ordinal);

    private JsonElement SanitizeAndAllowlist(JsonElement data, ToolDefinition definition)
    {
        if (data.ValueKind != JsonValueKind.Object) throw new ToolResultRejectedException("Formato inesperado.");
        var safe = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in data.EnumerateObject())
            if (definition.AllowedResultFields.Contains(property.Name)) safe[property.Name] = property.Value.Clone();
        if (safe.Count == 0) throw new ToolResultRejectedException("Resultado vazio após allowlist.");
        var text = sanitizer.Sanitize(JsonSerializer.Serialize(safe));
        try { using var document = JsonDocument.Parse(text); return document.RootElement.Clone(); }
        catch (JsonException ex) { throw new ToolResultRejectedException(ex.Message); }
    }
}

