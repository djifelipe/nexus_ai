using System.Diagnostics;
using System.Diagnostics.Metrics;
using AiGateway.Application;
using AiGateway.Domain;
using AiGateway.Domain.Tools;

namespace AiGateway.Infrastructure.Observability;

public sealed class AiTelemetry(ILogger<AiTelemetry> logger) : IAiTelemetry
{
    public const string SourceName = "AiGateway";
    private static readonly ActivitySource Activities = new(SourceName); private static readonly Meter Meter = new(SourceName);
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("ai_stage_duration_ms"); private static readonly Counter<long> Requests = Meter.CreateCounter<long>("ai_requests_total"); private static readonly Counter<long> Errors = Meter.CreateCounter<long>("ai_errors_total"); private static readonly Counter<long> RetrievalEvents = Meter.CreateCounter<long>("ai_retrieval_events_total"); private static readonly Counter<long> ToolCalls = Meter.CreateCounter<long>("ai_tool_calls_total"); private static readonly Histogram<double> ToolDuration = Meter.CreateHistogram<double>("ai_tool_duration_ms");
    public IDisposable StartRequest(AiRequest request) { Requests.Add(1); var activity = Activities.StartActivity("ai.request"); activity?.SetTag("ai.request_id", request.RequestId); activity?.SetTag("ai.conversation_id", request.ConversationId); return activity is null ? NullScope.Instance : activity; }
    public IDisposable StartStage(string stage) => new TimedStage(stage, Activities.StartActivity($"ai.{stage}"));
    public void RecordCompleted(AiResponse response) { try { logger.LogInformation("AI request {RequestId} completed with {Status}, module {Module}, sources {SourceCount}, tokens {PromptTokens}/{CompletionTokens}", response.RequestId, response.Status, response.Intent.Module ?? "Unknown", response.Sources.Count, response.Metrics.PromptTokens, response.Metrics.CompletionTokens); } catch { } }
    public void RecordError(string code) { try { Errors.Add(1, new KeyValuePair<string, object?>("error.code", code)); logger.LogWarning("AI request failed with code {ErrorCode}", code); } catch { } }
    public IDisposable StartRetrievalStage(string stage, IReadOnlyDictionary<string, object?>? tags = null) { var activity = Activities.StartActivity($"ai.retrieval.{stage}"); if (tags is not null) foreach (var tag in tags) activity?.SetTag(tag.Key, tag.Value); return activity is null ? NullScope.Instance : activity; }
    public void RecordRetrievalEvent(string operation, string outcome, double durationMs, int count = 0) { try { var tags = new[] { new KeyValuePair<string, object?>("operation", operation), new KeyValuePair<string, object?>("outcome", outcome) }; Duration.Record(durationMs, tags); RetrievalEvents.Add(Math.Max(1, count), tags); } catch { } }
    public IDisposable StartTool(ToolExecutionRequest request, ToolDefinition? definition) { try { var activity = Activities.StartActivity("ai.tool.execute"); activity?.SetTag("ai.request_id", request.RequestId); activity?.SetTag("ai.conversation_id", request.ConversationId); activity?.SetTag("tool.name", definition?.Name ?? "unregistered"); activity?.SetTag("tool.risk", definition?.RiskLevel.ToString() ?? "unknown"); return activity is null ? NullScope.Instance : activity; } catch { return NullScope.Instance; } }
    public void RecordTool(ToolExecutionRequest request, ToolExecutionResult result, ToolRiskLevel riskLevel) { try { var tags = new[] { new KeyValuePair<string, object?>("tool.name", ReadOnlyToolNames.All.Contains(result.ToolName) ? result.ToolName : "unregistered"), new KeyValuePair<string, object?>("tool.risk", riskLevel.ToString()), new KeyValuePair<string, object?>("tool.outcome", result.Success ? "success" : "failure"), new KeyValuePair<string, object?>("error.code", result.ErrorCode ?? "none") }; ToolCalls.Add(1, tags); ToolDuration.Record(result.DurationMs, tags); logger.LogInformation("Tool {ToolName} risk {RiskLevel} for request {RequestId}, company {CompanyId}, user {UserId} completed success {Success}, duration {DurationMs}, error {ErrorCode}", result.ToolName, riskLevel, request.RequestId, request.UserContext.CompanyId, request.UserContext.UserId, result.Success, result.DurationMs, result.ErrorCode ?? "none"); } catch { } }
    private sealed class TimedStage(string stage, Activity? activity) : IDisposable { private readonly long _started = Stopwatch.GetTimestamp(); public void Dispose() { Duration.Record(Stopwatch.GetElapsedTime(_started).TotalMilliseconds, new KeyValuePair<string, object?>("stage", stage)); activity?.Dispose(); } }
    private sealed class NullScope : IDisposable { public static readonly NullScope Instance = new(); public void Dispose() { } }
}
