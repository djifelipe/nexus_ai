using System.Text.Json;
using AiGateway.Application;
using AiGateway.Domain.Tools;
using Microsoft.Extensions.Options;

namespace AiGateway.Infrastructure.Mcp;

public sealed class WorkflowMcpReadAdapter(IWorkflowMcpTransport transport, IOptions<WorkflowToolMcpOptions> options) : IWorkflowReadPort
{
    public async Task<WorkflowResult?> GetWorkflowAsync(WorkflowQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Operation) || options.Value.Operation.Contains("sql", StringComparison.OrdinalIgnoreCase)) throw new ToolDependencyException("Operação MCP não permitida.");
        var row = await transport.InvokeAsync(options.Value.Operation, new Dictionary<string, object?>
        {
            ["companyId"] = query.CompanyId,
            ["erpVersion"] = query.ErpVersion,
            ["language"] = query.Language,
            ["permissions"] = query.Permissions.ToArray(),
            ["module"] = query.Module,
            ["feature"] = query.Feature,
            ["action"] = query.Action,
            ["publicationStatus"] = "published"
        }, ct);
        if (row.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (!row.TryGetProperty("published", out var published) || published.ValueKind != JsonValueKind.True) throw new ToolResultRejectedException("Workflow não publicado.");
        var steps = row.TryGetProperty("steps", out var values) && values.ValueKind == JsonValueKind.Array ? values.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray() : [];
        if (steps.Length == 0) throw new ToolResultRejectedException("Workflow sem passos válidos.");
        return new(Required(row, "sourceId"), query.Module, query.Feature, query.Action, Required(row, "version"), steps);
    }
    private static string Required(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new ToolResultRejectedException($"Campo obrigatório ausente: {name}");
}
