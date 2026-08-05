using System.Text.Json;

namespace AiGateway.Domain.Tools;

public enum ToolRiskLevel { ReadOnly, Validation, LowRiskWrite, HighRiskWrite }

public static class ReadOnlyToolNames
{
    public const string InventoryGetBalance = "inventory.getBalance";
    public const string InvoiceGetStatus = "invoice.getStatus";
    public const string PermissionCheck = "permission.check";
    public const string WorkflowGet = "workflow.get";
    public const string CustomerGetSummary = "customer.getSummary";
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    { InventoryGetBalance, InvoiceGetStatus, PermissionCheck, WorkflowGet, CustomerGetSummary };
}

public static class ToolErrorCodes
{
    public const string NotRegistered = "tool_not_registered";
    public const string InvalidArguments = "invalid_arguments";
    public const string AccessDenied = "access_denied";
    public const string NotFound = "not_found";
    public const string Timeout = "timeout";
    public const string DependencyUnavailable = "dependency_unavailable";
    public const string ResultRejected = "result_rejected";
    public const string LimitExceeded = "limit_exceeded";
    public const string Cancelled = "cancelled";
}

public sealed record ToolDefinition(
    string Name,
    string Description,
    JsonDocument InputSchema,
    ToolRiskLevel RiskLevel,
    bool RequiresConfirmation,
    IReadOnlyList<string> RequiredPermissions,
    IReadOnlySet<string> AllowedResultFields);

public sealed record ToolCall(string Id, string Name, JsonElement Arguments);

public sealed record ToolExecutionRequest(
    string RequestId,
    string TraceId,
    string? ConversationId,
    UserContext UserContext,
    ToolCall Call);

public sealed record ToolExecutionResult(
    string CallId,
    string ToolName,
    bool Success,
    JsonElement? Data,
    string? ErrorCode,
    string? SafeMessage,
    long DurationMs)
{
    public static ToolExecutionResult Failed(ToolCall call, string code, string message, long durationMs = 0) =>
        new(call.Id, call.Name, false, null, code, message, durationMs);
}

public sealed record InventoryBalanceQuery(string CompanyId, string ProductId, string? EstablishmentId, string? WarehouseId);
public sealed record InventoryBalanceResult(string ProductId, decimal AvailableBalance, string Unit, string? EstablishmentId, string? WarehouseId);
public sealed record InvoiceStatusQuery(string CompanyId, string DocumentType, string DocumentId);
public sealed record InvoiceStatusResult(string DocumentId, string DocumentType, string Status, DateTimeOffset? StatusAt, string? SafeReason);
public sealed record PermissionQuery(string CompanyId, string UserId, string PermissionCode);
public sealed record PermissionResult(string PermissionCode, bool Allowed, string Scope);
public sealed record WorkflowQuery(string CompanyId, string ErpVersion, string Language, IReadOnlySet<string> Permissions, string Module, string Feature, string Action);
public sealed record WorkflowResult(string SourceId, string Module, string Feature, string Action, string Version, IReadOnlyList<string> Steps);
public sealed record CustomerSummaryQuery(string CompanyId, string CustomerId, string ErpVersion);
public sealed record CustomerSummaryResult(string CustomerId, string DisplayName, string Status, string? City, string? State, IReadOnlyDictionary<string, string> AdditionalFields);

public sealed class ToolDependencyException(string safeMessage, Exception? inner = null) : Exception(safeMessage, inner);
public sealed class ToolRecordNotFoundException(string safeMessage) : Exception(safeMessage);
public sealed class ToolResultRejectedException(string safeMessage) : Exception(safeMessage);

