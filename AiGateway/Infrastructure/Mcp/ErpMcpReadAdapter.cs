using System.Globalization;
using System.Text.Json;
using AiGateway.Application;
using AiGateway.Domain.Tools;
using Microsoft.Extensions.Options;

namespace AiGateway.Infrastructure.Mcp;

public sealed class ErpMcpReadAdapter(IErpMcpTransport transport, IOptions<ErpMcpOptions> options) : IErpReadPort, IErpMcpClient
{
    public async Task<InventoryBalanceResult?> GetInventoryBalanceAsync(InventoryBalanceQuery query, CancellationToken ct)
    {
        var row = await Invoke(options.Value.InventoryOperation, new Dictionary<string, object?> { ["companyId"] = query.CompanyId, ["productId"] = query.ProductId, ["establishmentId"] = query.EstablishmentId, ["warehouseId"] = query.WarehouseId }, ct);
        return IsNull(row) ? null : new(query.ProductId, Decimal(row, "availableBalance"), Required(row, "unit"), query.EstablishmentId, query.WarehouseId);
    }

    public async Task<InvoiceStatusResult?> GetInvoiceStatusAsync(InvoiceStatusQuery query, CancellationToken ct)
    {
        var row = await Invoke(options.Value.InvoiceOperation, new Dictionary<string, object?> { ["companyId"] = query.CompanyId, ["documentType"] = query.DocumentType, ["documentId"] = query.DocumentId }, ct);
        return IsNull(row) ? null : new(query.DocumentId, query.DocumentType, Required(row, "status"), Date(row, "statusAt"), Optional(row, "safeReason"));
    }

    public async Task<PermissionResult> CheckPermissionAsync(PermissionQuery query, CancellationToken ct)
    {
        var row = await Invoke(options.Value.PermissionOperation, new Dictionary<string, object?> { ["companyId"] = query.CompanyId, ["userId"] = query.UserId, ["permissionCode"] = query.PermissionCode }, ct);
        return new(query.PermissionCode, Boolean(row, "allowed"), Optional(row, "scope") ?? "company");
    }

    public async Task<CustomerSummaryResult?> GetCustomerSummaryAsync(CustomerSummaryQuery query, CancellationToken ct)
    {
        var row = await Invoke(options.Value.CustomerOperation, new Dictionary<string, object?> { ["companyId"] = query.CompanyId, ["customerId"] = query.CustomerId, ["erpVersion"] = query.ErpVersion }, ct);
        if (IsNull(row)) return null;
        var known = new HashSet<string>(["customerId", "displayName", "status", "city", "state"], StringComparer.OrdinalIgnoreCase);
        var additional = row.EnumerateObject().Where(x => !known.Contains(x.Name) && x.Value.ValueKind == JsonValueKind.String).ToDictionary(x => x.Name, x => x.Value.GetString() ?? "", StringComparer.OrdinalIgnoreCase);
        return new(query.CustomerId, Required(row, "displayName"), Required(row, "status"), Optional(row, "city"), Optional(row, "state"), additional);
    }

    private Task<JsonElement> Invoke(string operation, IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(operation) || operation.Contains("sql", StringComparison.OrdinalIgnoreCase)) throw new ToolDependencyException("Operação MCP não permitida.");
        return transport.InvokeAsync(operation, args, ct);
    }
    private static bool IsNull(JsonElement row) => row.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;
    private static string Required(JsonElement row, string name) => Optional(row, name) ?? throw new ToolResultRejectedException($"Campo obrigatório ausente: {name}");
    private static string? Optional(JsonElement row, string name) => row.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : null;
    private static decimal Decimal(JsonElement row, string name) => row.TryGetProperty(name, out var value) && decimal.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : throw new ToolResultRejectedException($"Campo inválido: {name}");
    private static bool Boolean(JsonElement row, string name) => row.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || bool.TryParse(value.ToString(), out var result) && result);
    private static DateTimeOffset? Date(JsonElement row, string name) => DateTimeOffset.TryParse(Optional(row, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result) ? result : null;
}

