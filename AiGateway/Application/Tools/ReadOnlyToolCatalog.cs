using System.Collections.ObjectModel;
using System.Text.Json;
using AiGateway.Domain.Tools;
using Microsoft.Extensions.Options;

namespace AiGateway.Application.Tools;

public sealed class ReadOnlyToolCatalog : IToolCatalog
{
    private readonly IReadOnlyDictionary<string, ToolDefinition> _enabled;
    public IReadOnlyList<ToolDefinition> Enabled { get; }

    public ReadOnlyToolCatalog(IOptions<ReadOnlyToolsOptions> options)
    {
        var definitions = CreateDefinitions().ToDictionary(x => x.Name, StringComparer.Ordinal);
        var configured = options.Value.Enabled.ToHashSet(StringComparer.Ordinal);
        var unknown = configured.Where(x => !definitions.ContainsKey(x)).ToArray();
        if (unknown.Length > 0) throw new OptionsValidationException(ReadOnlyToolsOptions.SectionName, typeof(ReadOnlyToolsOptions), unknown.Select(x => $"Ferramenta desconhecida: {x}"));
        _enabled = new ReadOnlyDictionary<string, ToolDefinition>(definitions.Where(x => configured.Contains(x.Key)).ToDictionary(StringComparer.Ordinal));
        Enabled = _enabled.Values.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
    }

    public bool TryGet(string name, out ToolDefinition definition) => _enabled.TryGetValue(name, out definition!);

    internal static IReadOnlyList<ToolDefinition> CreateDefinitions() =>
    [
        Definition(ReadOnlyToolNames.InventoryGetBalance, "Consulta o saldo disponível de um produto.", ["productId"], ["productId", "establishmentId", "warehouseId", "companyId", "userId"], ["Inventory.Balance.View"], ["productId", "availableBalance", "unit", "establishmentId", "warehouseId"]),
        Definition(ReadOnlyToolNames.InvoiceGetStatus, "Consulta a situação atual de um documento.", ["documentType", "documentId"], ["documentType", "documentId", "companyId", "userId"], ["Invoice.Status.View"], ["documentId", "documentType", "status", "statusAt", "safeReason"]),
        Definition(ReadOnlyToolNames.PermissionCheck, "Consulta uma decisão de permissão para o usuário autenticado.", ["permissionCode"], ["permissionCode", "companyId", "userId"], ["Security.Permission.View"], ["permissionCode", "allowed", "scope"]),
        Definition(ReadOnlyToolNames.WorkflowGet, "Obtém um workflow publicado e autorizado.", ["module", "feature", "action"], ["module", "feature", "action", "companyId", "userId"], ["Knowledge.Workflow.View"], ["sourceId", "module", "feature", "action", "version", "steps"]),
        Definition(ReadOnlyToolNames.CustomerGetSummary, "Consulta dados cadastrais resumidos de um cliente.", ["customerId"], ["customerId", "companyId", "userId"], ["Customer.Summary.View"], ["customerId", "displayName", "status", "city", "state", "additionalFields"])
    ];

    private static ToolDefinition Definition(string name, string description, string[] required, string[] properties, string[] permissions, string[] resultFields)
    {
        var schema = JsonSerializer.Serialize(new
        {
            type = "object",
            additionalProperties = false,
            required,
            properties = properties.ToDictionary(x => x, _ => new { type = "string", minLength = 1 }, StringComparer.Ordinal)
        });
        return new(name, description, JsonDocument.Parse(schema), ToolRiskLevel.ReadOnly, false, permissions, resultFields.ToHashSet(StringComparer.Ordinal));
    }
}

