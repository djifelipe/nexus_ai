using System.Text.Json;
using AiGateway.Application;
using AiGateway.Application.Orchestration;
using AiGateway.Application.Tools;
using AiGateway.Application.Validation;
using AiGateway.Domain;
using AiGateway.Domain.Tools;
using AiGateway.Infrastructure.Security;
using Microsoft.Extensions.Options;
using AiGateway.Domain.Policies;
using AiGateway.Domain.Responses;

namespace AiGateway.Tests;

public sealed class ToolOrchestrationTests
{
    [Fact]
    public async Task Orchestrator_executes_tool_and_validates_traceable_result()
    {
        var calls = new[] { Call("c1", ReadOnlyToolNames.InventoryGetBalance, new { productId = "p1" }) };
        var model = new QueueModel([
            new ModelResponse("",1,1,"tool_calls",true,1){ToolCalls=calls},
            new ModelResponse("Saldo disponível: 10 UN [tool:c1]",2,2,"stop",false,1)
        ]);
        var executor = new FakeExecutor();
        var response = await Orchestrator(model, executor).ExecuteAsync(Request(), default);
        Assert.Equal(ValidationStatus.Grounded, response.Status); Assert.Single(executor.Calls); Assert.Contains("tool:c1", response.Sources.Select(x => x.SourceId)); Assert.Equal(2, model.Calls);
        Assert.Contains("UNTRUSTED DATA", model.Prompts[1].Messages.Last(x => x.Role == "tool").Content);
    }

    [Fact]
    public async Task Orchestrator_rejects_prohibited_tool_without_reinterpretation()
    {
        var model = new QueueModel([new ModelResponse("", 1, 1, "tool_calls", true, 1) { ToolCalls = [Call("c1", "invoice.cancel", new { documentId = "1" })] }]);
        var error = await Assert.ThrowsAsync<AiGatewayException>(() => Orchestrator(model, new FakeExecutor()).ExecuteAsync(Request(), default));
        Assert.Equal(ErrorCodes.UnsupportedTool, error.Code);
    }

    [Fact]
    public async Task Orchestrator_enforces_repetition_limit_before_third_execution()
    {
        var responses = Enumerable.Range(1, 3).Select(i => new ModelResponse("", 1, 1, "tool_calls", true, 1) { ToolCalls = [Call($"c{i}", ReadOnlyToolNames.InventoryGetBalance, new { productId = "p1" })] }).ToArray();
        var executor = new FakeExecutor();
        var error = await Assert.ThrowsAsync<AiGatewayException>(() => Orchestrator(new QueueModel(responses), executor).ExecuteAsync(Request(), default));
        Assert.Equal(ErrorCodes.ToolLimitExceeded, error.Code); Assert.Equal(2, executor.Calls.Count);
    }

    [Fact]
    public async Task Orchestrator_enforces_global_limit_before_sixth_execution()
    {
        var names = ReadOnlyToolNames.All.ToArray();
        var first = new ModelResponse("", 1, 1, "tool_calls", true, 1) { ToolCalls = names.Select((x, i) => Call($"c{i}", x, Arguments(x))).ToArray() };
        var second = new ModelResponse("", 1, 1, "tool_calls", true, 1) { ToolCalls = [Call("c6", names[0], Arguments(names[0]))] };
        var executor = new FakeExecutor();
        var error = await Assert.ThrowsAsync<AiGatewayException>(() => Orchestrator(new QueueModel([first, second]), executor).ExecuteAsync(Request(), default));
        Assert.Equal(ErrorCodes.ToolLimitExceeded, error.Code); Assert.Equal(5, executor.Calls.Count);
    }

    [Fact]
    public async Task Orchestrator_regenerates_once_and_revalidates_without_expanding_sources()
    {
        var model = new QueueModel([
            new ModelResponse("Resposta parcial [kb1]", 1, 1, "stop", false, 1),
            new ModelResponse("Resposta corrigida [kb1]", 1, 1, "stop", false, 1)
        ]);
        var validator = new SequenceValidator();
        var orchestrator = OrchestratorWithAdvancedValidation(model, new FakeExecutor(), validator);
        var response = await orchestrator.ExecuteAsync(Request(), default);
        Assert.Equal(ValidationStatus.Grounded, response.Status); Assert.Equal(2, model.Calls); Assert.Equal(2, validator.Calls);
        Assert.Equal(model.Prompts[0].Sources.Select(x => x.Id), model.Prompts[1].Sources.Select(x => x.Id));
        Assert.Contains("VALIDATION FEEDBACK - SANITIZED", model.Prompts[1].Messages.Last().Content);
    }

    private static object Arguments(string name) => name switch
    {
        ReadOnlyToolNames.InventoryGetBalance => new { productId = "p" },
        ReadOnlyToolNames.InvoiceGetStatus => new { documentType = "NFe", documentId = "d" },
        ReadOnlyToolNames.PermissionCheck => new { permissionCode = "p" },
        ReadOnlyToolNames.WorkflowGet => new { module = "Fiscal", feature = "NFe", action = "View" },
        _
        => new { customerId = "c" }
    };
    private static ToolCall Call(string id, string name, object args) => new(id, name, JsonSerializer.SerializeToElement(args));
    private static AiRequest Request()
    {
        var permissions = new[] { "Inventory.Balance.View", "Invoice.Status.View", "Security.Permission.View", "Knowledge.Workflow.View", "Customer.Summary.View" }.ToHashSet();
        var user = new UserContext("company", "user", "1", "pt-BR", permissions, new("Estoque", null, null));
        return new("conversation", "saldo do produto", "company", "user", user.Screen, false, true, user, "request", "trace");
    }
    private static AiOrchestrator Orchestrator(ILanguageModelClient model, IToolExecutor executor)
    {
        var catalog = new ReadOnlyToolCatalog(Options.Create(new ReadOnlyToolsOptions { Enabled = ReadOnlyToolNames.All.ToArray() }));
        return new(new Router(), new Retriever(), new PromptBuilder(), model, new CitationResponseValidator(), new Telemetry(), Options.Create(new AiGatewayOptions { TotalTimeoutSeconds = 30 }), Options.Create(new AdvancedRetrievalOptions()), null!, null!, null!, new SensitiveDataSanitizer(), catalog, executor, Options.Create(new ReadOnlyToolsOptions()));
    }
    private static AiOrchestrator OrchestratorWithAdvancedValidation(ILanguageModelClient model, IToolExecutor executor, IResponseValidator validator)
    {
        var catalog = new ReadOnlyToolCatalog(Options.Create(new ReadOnlyToolsOptions { Enabled = ReadOnlyToolNames.All.ToArray() }));
        return new(new Router(), new Retriever(), new PromptBuilder(), model, validator, new Telemetry(), Options.Create(new AiGatewayOptions { TotalTimeoutSeconds = 30 }), Options.Create(new AdvancedRetrievalOptions()), null!, null!, null!, new SensitiveDataSanitizer(), catalog, executor, Options.Create(new ReadOnlyToolsOptions()), Options.Create(new AdvancedValidationOptions { Enabled = true, RegenerationEnabled = true }), new AdvancedValidationPolicy());
    }
    private sealed class Router : IIntentRouter { public Task<IntentResult> RouteAsync(IntentRouterRequest request, CancellationToken ct) => Task.FromResult(new IntentResult("Estoque", "Produto", "Saldo", "Produto", IntentType.DataQuery, .9, ["saldo"], [ReadOnlyToolNames.InventoryGetBalance], false, null, "test", ["Estoque"])); }
    private sealed class Retriever : IKnowledgeRetriever { public Task<RetrievalResult> RetrieveAsync(RetrievalRequest request, CancellationToken ct) => Task.FromResult(new RetrievalResult([new("kb1", "documentation", "Saldo", "Use o saldo atual retornado pela ferramenta.", "Estoque", "Produto", "1", 0, 1, 1, true, new Dictionary<string, string>())], new([], [], 1, false, false))); }
    private sealed class PromptBuilder : IPromptBuilder { public Task<PromptPackage> BuildAsync(PromptBuildRequest request, CancellationToken ct) => Task.FromResult(new PromptPackage([new("system", "cite fontes"), new("user", request.Question)], request.Retrieval.Items, 10, request.Question)); }
    private sealed class QueueModel(IEnumerable<ModelResponse> responses) : ILanguageModelClient
    {
        private readonly Queue<ModelResponse> _responses = new(responses); public int Calls { get; private set; }
        public List<PromptPackage> Prompts { get; } = [];
        public Task<ModelResponse> ChatAsync(PromptPackage prompt, CancellationToken ct) { Calls++; Prompts.Add(prompt); return Task.FromResult(_responses.Dequeue()); }
    }
    private sealed class FakeExecutor : IToolExecutor
    {
        public List<ToolExecutionRequest> Calls { get; } = [];
        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken ct) { Calls.Add(request); if (!ReadOnlyToolNames.All.Contains(request.Call.Name)) return Task.FromResult(ToolExecutionResult.Failed(request.Call, ToolErrorCodes.NotRegistered, "denied")); var data = JsonSerializer.SerializeToElement(new { productId = "p1", availableBalance = 10, unit = "UN" }); return Task.FromResult(new ToolExecutionResult(request.Call.Id, request.Call.Name, true, data, null, null, 1)); }
    }
    private sealed class SequenceValidator : IResponseValidator
    {
        public int Calls { get; private set; }
        public Task<ResponseValidationResult> ValidateAsync(ResponseValidationRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls == 1) return Task.FromResult(new ResponseValidationResult(ValidationStatus.PartiallyGrounded, request.ModelResponse.Content, ["kb1"], [ErrorCodes.UnsupportedClaim]) { SanitizedReasons = [new(ErrorCodes.UnsupportedClaim, "grounding", true)], RegenerationRecommended = true });
            return Task.FromResult(new ResponseValidationResult(ValidationStatus.Grounded, request.ModelResponse.Content, ["kb1"], []) { Confidence = .9, ScoreComponents = new(1, 1, 1, 1) });
        }
    }
    private sealed class Telemetry : IAiTelemetry
    {
        public IDisposable StartRequest(AiRequest request) => Scope.Instance; public IDisposable StartStage(string stage) => Scope.Instance; public void RecordCompleted(AiResponse response) { }
        public void RecordError(string code) { }
        public IDisposable StartRetrievalStage(string stage, IReadOnlyDictionary<string, object?>? tags = null) => Scope.Instance; public void RecordRetrievalEvent(string operation, string outcome, double durationMs, int count = 0) { }
        public IDisposable StartTool(ToolExecutionRequest request, ToolDefinition? definition) => Scope.Instance; public void RecordTool(ToolExecutionRequest request, ToolExecutionResult result, ToolRiskLevel riskLevel) { }
        private sealed class Scope : IDisposable { public static readonly Scope Instance = new(); public void Dispose() { } }
    }
}
