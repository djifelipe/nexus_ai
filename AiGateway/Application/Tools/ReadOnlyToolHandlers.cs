using System.Text.Json;
using AiGateway.Domain;
using AiGateway.Domain.Tools;
using Microsoft.Extensions.Options;

namespace AiGateway.Application.Tools;

public sealed class InventoryBalanceToolHandler(IErpReadPort port) : IToolHandler
{
    public string Name => ReadOnlyToolNames.InventoryGetBalance;
    public async Task<JsonElement> ExecuteAsync(UserContext user, JsonElement args, CancellationToken ct) => JsonSerializer.SerializeToElement(
        await port.GetInventoryBalanceAsync(new(user.CompanyId, Required(args, "productId"), Optional(args, "establishmentId"), Optional(args, "warehouseId")), ct)
        ?? throw new ToolRecordNotFoundException("Saldo não encontrado."), JsonDefaults.Options);
    internal static string Required(JsonElement value, string name) => value.GetProperty(name).GetString()!;
    internal static string? Optional(JsonElement value, string name) => value.TryGetProperty(name, out var result) ? result.GetString() : null;
}

public sealed class InvoiceStatusToolHandler(IErpReadPort port) : IToolHandler
{
    public string Name => ReadOnlyToolNames.InvoiceGetStatus;
    public async Task<JsonElement> ExecuteAsync(UserContext user, JsonElement args, CancellationToken ct) => JsonSerializer.SerializeToElement(
        await port.GetInvoiceStatusAsync(new(user.CompanyId, InventoryBalanceToolHandler.Required(args, "documentType"), InventoryBalanceToolHandler.Required(args, "documentId")), ct)
        ?? throw new ToolRecordNotFoundException("Documento não encontrado."), JsonDefaults.Options);
}

public sealed class PermissionCheckToolHandler(IErpReadPort port) : IToolHandler
{
    public string Name => ReadOnlyToolNames.PermissionCheck;
    public async Task<JsonElement> ExecuteAsync(UserContext user, JsonElement args, CancellationToken ct) => JsonSerializer.SerializeToElement(
        await port.CheckPermissionAsync(new(user.CompanyId, user.UserId, InventoryBalanceToolHandler.Required(args, "permissionCode")), ct), JsonDefaults.Options);
}

public sealed class WorkflowToolHandler(IWorkflowReadPort port) : IToolHandler
{
    public string Name => ReadOnlyToolNames.WorkflowGet;
    public async Task<JsonElement> ExecuteAsync(UserContext user, JsonElement args, CancellationToken ct) => JsonSerializer.SerializeToElement(
        await port.GetWorkflowAsync(new(user.CompanyId, user.ErpVersion, user.Language, user.Permissions, InventoryBalanceToolHandler.Required(args, "module"), InventoryBalanceToolHandler.Required(args, "feature"), InventoryBalanceToolHandler.Required(args, "action")), ct)
        ?? throw new ToolRecordNotFoundException("Workflow não encontrado."), JsonDefaults.Options);
}

public sealed class CustomerSummaryToolHandler(IErpReadPort port, IOptions<ReadOnlyToolsOptions> options) : IToolHandler
{
    public string Name => ReadOnlyToolNames.CustomerGetSummary;
    public async Task<JsonElement> ExecuteAsync(UserContext user, JsonElement args, CancellationToken ct)
    {
        var result = await port.GetCustomerSummaryAsync(new(user.CompanyId, InventoryBalanceToolHandler.Required(args, "customerId"), user.ErpVersion), ct)
            ?? throw new ToolRecordNotFoundException("Cliente não encontrado.");
        var allowed = options.Value.CustomerSummaryAllowedFields.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var projected = result with { AdditionalFields = result.AdditionalFields.Where(x => allowed.Contains(x.Key)).ToDictionary(StringComparer.OrdinalIgnoreCase) };
        return JsonSerializer.SerializeToElement(projected, JsonDefaults.Options);
    }
}

internal static class JsonDefaults
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
