using System.Text.Json;
using AiGateway.Application;
using AiGateway.Application.Tools;
using AiGateway.Application.IntentRouting;
using AiGateway.Domain;
using AiGateway.Domain.Tools;
using AiGateway.Infrastructure.Mcp;
using Microsoft.Extensions.Options;

namespace AiGateway.Tests;

public sealed class ReadOnlyToolTests
{
    private static readonly string[] AllPermissions = ["Inventory.Balance.View", "Invoice.Status.View", "Security.Permission.View", "Knowledge.Workflow.View", "Customer.Summary.View"];

    [Fact]
    public void Catalog_resolves_only_exact_enabled_read_only_names()
    {
        var catalog = Catalog(ReadOnlyToolNames.All.ToArray());
        Assert.Equal(5, catalog.Enabled.Count);
        Assert.All(catalog.Enabled, x => { Assert.Equal(ToolRiskLevel.ReadOnly, x.RiskLevel); Assert.False(x.RequiresConfirmation); Assert.Equal(JsonValueKind.Object, x.InputSchema.RootElement.ValueKind); });
        Assert.True(catalog.TryGet(ReadOnlyToolNames.InventoryGetBalance, out _));
        Assert.False(catalog.TryGet("Inventory.GetBalance", out _));
        Assert.False(catalog.TryGet("invoice.cancel", out _));
    }

    [Fact]
    public async Task Intent_router_emits_only_enabled_exact_tool_and_preserves_disabled_fallback()
    {
        var user = User(AllPermissions);
        var enabled = new RuleBasedIntentRouter(new InventoryIntentCatalog(), Options.Create(new AiGatewayOptions()), Catalog(ReadOnlyToolNames.InventoryGetBalance));
        var routed = await enabled.RouteAsync(new("qual o saldo do produto", user), default);
        Assert.Equal([ReadOnlyToolNames.InventoryGetBalance], routed.RequiredTools);
        var disabled = new RuleBasedIntentRouter(new InventoryIntentCatalog(), Options.Create(new AiGatewayOptions()), Catalog());
        Assert.Empty((await disabled.RouteAsync(new("qual o saldo do produto", user), default)).RequiredTools);
    }

    [Fact]
    public async Task Executor_rejects_unknown_invalid_cross_tenant_and_unauthorized_before_handler()
    {
        var handler = new CapturingHandler(ReadOnlyToolNames.InventoryGetBalance, JsonSerializer.SerializeToElement(new { productId = "p", availableBalance = 1, unit = "UN" }));
        var executor = Executor(Catalog(ReadOnlyToolNames.InventoryGetBalance), [handler]);
        var user = User(AllPermissions);
        Assert.Equal(ToolErrorCodes.NotRegistered, (await executor.ExecuteAsync(Request(user, "invoice.cancel", new { documentId = "1" }), default)).ErrorCode);
        Assert.Equal(ToolErrorCodes.InvalidArguments, (await executor.ExecuteAsync(Request(user, ReadOnlyToolNames.InventoryGetBalance, new { }), default)).ErrorCode);
        Assert.Equal(ToolErrorCodes.AccessDenied, (await executor.ExecuteAsync(Request(user, ReadOnlyToolNames.InventoryGetBalance, new { productId = "p", companyId = "other" }), default)).ErrorCode);
        Assert.Equal(ToolErrorCodes.AccessDenied, (await executor.ExecuteAsync(Request(User([]), ReadOnlyToolNames.InventoryGetBalance, new { productId = "p" }), default)).ErrorCode);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Executor_maps_timeout_cancellation_dependency_not_found_and_rejected_result()
    {
        var user = User(AllPermissions);
        var timeout = Executor(Catalog(ReadOnlyToolNames.InventoryGetBalance), [new DelayedHandler()], timeoutSeconds: 1);
        Assert.Equal(ToolErrorCodes.Timeout, (await timeout.ExecuteAsync(Request(user, ReadOnlyToolNames.InventoryGetBalance, new { productId = "p" }), default)).ErrorCode);

        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Equal(ToolErrorCodes.Cancelled, (await timeout.ExecuteAsync(Request(user, ReadOnlyToolNames.InventoryGetBalance, new { productId = "p" }), cancelled.Token)).ErrorCode);

        foreach (var (exception, code) in new (Exception, string)[] { (new ToolDependencyException("raw secret"), ToolErrorCodes.DependencyUnavailable), (new ToolRecordNotFoundException("raw"), ToolErrorCodes.NotFound) })
        {
            var executor = Executor(Catalog(ReadOnlyToolNames.InventoryGetBalance), [new ThrowingHandler(exception)]);
            Assert.Equal(code, (await executor.ExecuteAsync(Request(user, ReadOnlyToolNames.InventoryGetBalance, new { productId = "p" }), default)).ErrorCode);
        }
        var rejected = Executor(Catalog(ReadOnlyToolNames.InventoryGetBalance), [new CapturingHandler(ReadOnlyToolNames.InventoryGetBalance, JsonSerializer.SerializeToElement(new { password = "secret" }))]);
        var result = await rejected.ExecuteAsync(Request(user, ReadOnlyToolNames.InventoryGetBalance, new { productId = "p" }), default);
        Assert.Equal(ToolErrorCodes.ResultRejected, result.ErrorCode); Assert.Null(result.Data);
    }

    [Fact]
    public async Task Five_handlers_return_minimal_allowlisted_results()
    {
        var erp = new FakeErpPort(); var workflow = new FakeWorkflowPort();
        IToolHandler[] handlers = [new InventoryBalanceToolHandler(erp), new InvoiceStatusToolHandler(erp), new PermissionCheckToolHandler(erp), new WorkflowToolHandler(workflow), new CustomerSummaryToolHandler(erp, Options.Create(new ReadOnlyToolsOptions { CustomerSummaryAllowedFields = ["nickname"] }))];
        var executor = Executor(Catalog(ReadOnlyToolNames.All.ToArray()), handlers);
        var cases = new[]
        {
            Request(User(AllPermissions), ReadOnlyToolNames.InventoryGetBalance, new { productId="p1", warehouseId="w1" }),
            Request(User(AllPermissions), ReadOnlyToolNames.InvoiceGetStatus, new { documentType="NFe", documentId="d1" }),
            Request(User(AllPermissions), ReadOnlyToolNames.PermissionCheck, new { permissionCode="Fiscal.View" }),
            Request(User(AllPermissions), ReadOnlyToolNames.WorkflowGet, new { module="Fiscal", feature="NFe", action="Consultar" }),
            Request(User(AllPermissions), ReadOnlyToolNames.CustomerGetSummary, new { customerId="c1" })
        };
        foreach (var request in cases) Assert.True((await executor.ExecuteAsync(request, default)).Success);
        var customer = await executor.ExecuteAsync(cases[^1], default);
        var json = customer.Data!.Value.GetRawText(); Assert.Contains("nickname", json); Assert.DoesNotContain("bankAccount", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(5, erp.Calls); Assert.Equal(1, workflow.Calls);
    }

    [Fact]
    public async Task Adapters_route_to_designated_allowlisted_operations_with_authenticated_scope()
    {
        var erpTransport = new FakeErpTransport(JsonSerializer.SerializeToElement(new { availableBalance = 3.5m, unit = "UN" }));
        var erp = new ErpMcpReadAdapter(erpTransport, Options.Create(new ErpMcpOptions()));
        var balance = await erp.GetInventoryBalanceAsync(new("company-1", "p1", null, null), default);
        Assert.Equal("inventory_get_balance", erpTransport.Operation); Assert.Equal("company-1", erpTransport.Arguments!["companyId"]); Assert.Equal(3.5m, balance!.AvailableBalance);

        var workflowTransport = new FakeWorkflowTransport(JsonSerializer.SerializeToElement(new { sourceId = "wf1", version = "1", published = true, steps = new[] { "A", "B" } }));
        var workflow = new WorkflowMcpReadAdapter(workflowTransport, Options.Create(new WorkflowToolMcpOptions()));
        var value = await workflow.GetWorkflowAsync(new("company-1", "1", "pt-BR", new HashSet<string> { "p" }, "Fiscal", "NFe", "Consultar"), default);
        Assert.Equal("workflow_get", workflowTransport.Operation); Assert.Equal("published", workflowTransport.Arguments!["publicationStatus"]); Assert.Equal("wf1", value!.SourceId);
    }

    [Fact]
    public async Task Adapters_reject_sql_named_operations()
    {
        var transport = new FakeErpTransport(JsonSerializer.SerializeToElement(new { }));
        var adapter = new ErpMcpReadAdapter(transport, Options.Create(new ErpMcpOptions { InventoryOperation = "execute_sql" }));
        await Assert.ThrowsAsync<ToolDependencyException>(() => adapter.GetInventoryBalanceAsync(new("c", "p", null, null), default));
        Assert.Null(transport.Operation);
    }

    private static ReadOnlyToolCatalog Catalog(params string[] enabled) => new(Options.Create(new ReadOnlyToolsOptions { Enabled = enabled }));
    private static ReadOnlyToolExecutor Executor(IToolCatalog catalog, IEnumerable<IToolHandler> handlers, int timeoutSeconds = 10) => new(catalog, handlers, new PassSanitizer(), new TestTelemetry(), Options.Create(new ReadOnlyToolsOptions { TimeoutSeconds = timeoutSeconds }));
    private static UserContext User(IEnumerable<string> permissions) => new("company-1", "user-1", "1", "pt-BR", permissions.ToHashSet(), new("Fiscal", null, null));
    private static ToolExecutionRequest Request(UserContext user, string name, object args) => new("request-1", "trace-1", "conversation-1", user, new("call-1", name, JsonSerializer.SerializeToElement(args)));

    private sealed class CapturingHandler(string name, JsonElement result) : IToolHandler { public string Name => name; public int Calls { get; private set; } public Task<JsonElement> ExecuteAsync(UserContext userContext, JsonElement arguments, CancellationToken cancellationToken) { Calls++; return Task.FromResult(result); } }
    private sealed class DelayedHandler : IToolHandler { public string Name => ReadOnlyToolNames.InventoryGetBalance; public async Task<JsonElement> ExecuteAsync(UserContext userContext, JsonElement arguments, CancellationToken cancellationToken) { await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken); return JsonSerializer.SerializeToElement(new { productId = "p", availableBalance = 1, unit = "UN" }); } }
    private sealed class ThrowingHandler(Exception exception) : IToolHandler { public string Name => ReadOnlyToolNames.InventoryGetBalance; public Task<JsonElement> ExecuteAsync(UserContext userContext, JsonElement arguments, CancellationToken cancellationToken) => Task.FromException<JsonElement>(exception); }
    private sealed class PassSanitizer : ISensitiveDataSanitizer { public string Sanitize(string input) => input.Replace("secret", "[REDACTED]", StringComparison.OrdinalIgnoreCase); }
    private sealed class TestTelemetry : IAiTelemetry
    {
        public IDisposable StartRequest(AiRequest request) => Noop.Instance; public IDisposable StartStage(string stage) => Noop.Instance; public void RecordCompleted(AiResponse response) { }
        public void RecordError(string code) { }
        public IDisposable StartRetrievalStage(string stage, IReadOnlyDictionary<string, object?>? tags = null) => Noop.Instance; public void RecordRetrievalEvent(string operation, string outcome, double durationMs, int count = 0) { }
        public IDisposable StartTool(ToolExecutionRequest request, ToolDefinition? definition) => Noop.Instance; public void RecordTool(ToolExecutionRequest request, ToolExecutionResult result, ToolRiskLevel riskLevel) { }
        private sealed class Noop : IDisposable { public static readonly Noop Instance = new(); public void Dispose() { } }
    }
    private sealed class FakeErpPort : IErpReadPort
    {
        public int Calls { get; private set; }
        public Task<InventoryBalanceResult?> GetInventoryBalanceAsync(InventoryBalanceQuery query, CancellationToken ct) { Calls++; return Task.FromResult<InventoryBalanceResult?>(new(query.ProductId, 10, "UN", query.EstablishmentId, query.WarehouseId)); }
        public Task<InvoiceStatusResult?> GetInvoiceStatusAsync(InvoiceStatusQuery query, CancellationToken ct) { Calls++; return Task.FromResult<InvoiceStatusResult?>(new(query.DocumentId, query.DocumentType, "Authorized", DateTimeOffset.UtcNow, null)); }
        public Task<PermissionResult> CheckPermissionAsync(PermissionQuery query, CancellationToken ct) { Calls++; return Task.FromResult(new PermissionResult(query.PermissionCode, true, "company")); }
        public Task<CustomerSummaryResult?> GetCustomerSummaryAsync(CustomerSummaryQuery query, CancellationToken ct) { Calls++; return Task.FromResult<CustomerSummaryResult?>(new(query.CustomerId, "Cliente", "Active", "São Paulo", "SP", new Dictionary<string, string> { { "nickname", "C" }, { "bankAccount", "123" } })); }
    }
    private sealed class FakeWorkflowPort : IWorkflowReadPort { public int Calls { get; private set; } public Task<WorkflowResult?> GetWorkflowAsync(WorkflowQuery query, CancellationToken ct) { Calls++; return Task.FromResult<WorkflowResult?>(new("wf1", query.Module, query.Feature, query.Action, "1", ["A", "B"])); } }
    private sealed class FakeErpTransport(JsonElement result) : IErpMcpTransport { public string? Operation { get; private set; } public IReadOnlyDictionary<string, object?>? Arguments { get; private set; } public Task<JsonElement> InvokeAsync(string operation, IReadOnlyDictionary<string, object?> arguments, CancellationToken ct) { Operation = operation; Arguments = arguments; return Task.FromResult(result); } }
    private sealed class FakeWorkflowTransport(JsonElement result) : IWorkflowMcpTransport { public string? Operation { get; private set; } public IReadOnlyDictionary<string, object?>? Arguments { get; private set; } public Task<JsonElement> InvokeAsync(string operation, IReadOnlyDictionary<string, object?> arguments, CancellationToken ct) { Operation = operation; Arguments = arguments; return Task.FromResult(result); } }
    private sealed class InventoryIntentCatalog : IIntentCatalog { public Task<IReadOnlyList<IntentCatalogEntry>> GetActiveAsync(string companyId, IReadOnlySet<string> permissions, CancellationToken ct) => Task.FromResult<IReadOnlyList<IntentCatalogEntry>>([new("Estoque", "Produto", "Saldo", "Produto", IntentType.DataQuery, ["saldo", "produto"], 1, null)]); }
}
