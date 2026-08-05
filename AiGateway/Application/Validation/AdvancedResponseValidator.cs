using System.Diagnostics;
using System.Text.RegularExpressions;
using AiGateway.Domain;
using AiGateway.Domain.Policies;
using AiGateway.Domain.Responses;
using Microsoft.Extensions.Options;

namespace AiGateway.Application.Validation;

public sealed partial class AdvancedResponseValidator(
    CitationResponseValidator basic,
    IClaimExtractor claims,
    ISemanticGroundingEvaluator grounding,
    ISensitiveDataDetector sensitiveData,
    AdvancedValidationPolicy policy,
    IOptions<AdvancedValidationOptions> options,
    IAiTelemetry telemetry) : IResponseValidator
{
    private const string SafeInsufficientAnswer = "Não encontrei informações suficientes na base de conhecimento para responder com segurança.";
    private const string SafeUnsafeAnswer = "Não posso fornecer essa resposta porque ela viola as políticas de segurança ou acesso.";

    public async Task<ResponseValidationResult> ValidateAsync(ResponseValidationRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return await basic.ValidateAsync(request, cancellationToken);
        var watch = Stopwatch.StartNew();
        var basicResult = await basic.ValidateAsync(request, cancellationToken);
        ResponseValidationResult advanced;
        if (request.ModelResponse.Content.Length > policy.MaxResponseCharacters)
            advanced = Result(ValidationStatus.RequiresReview, "Não foi possível validar a resposta dentro dos limites configurados.", [], ErrorCodes.ValidationLimitExceeded, true);
        else
        {
            var sensitive = sensitiveData.Detect(request.ModelResponse.Content, request.UserContext);
            var blocking = sensitive.Where(x => AdvancedValidationPolicy.BlockingCategories.Contains(x.Category)).ToArray();
            advanced = blocking.Length > 0
                ? Result(ValidationStatus.Unsafe, SafeUnsafeAnswer, [], ErrorCodes.SensitiveDataDetected, false) with { ContainsSensitiveData = true }
                : await ValidateGroundingAsync(request, basicResult, cancellationToken);
        }

        var effective = options.Value.ShadowModeEnabled ? basicResult : advanced;
        var supported = advanced.Claims.Count(x => x.Status == ClaimGroundingStatus.Supported);
        SafeTelemetry(new(request.RequestId, request.ConversationId, advanced.Status, ScoreBand(advanced.Confidence), policy.Version,
            advanced.Claims.Count, supported, advanced.Claims.Count - supported, advanced.ScoreComponents?.CitationCoverage ?? 0,
            advanced.SemanticOutcome, request.Attempt, request.Attempt > 0, advanced.SanitizedReasons.FirstOrDefault()?.Code, watch.Elapsed.TotalMilliseconds));
        return effective;
    }

    private async Task<ResponseValidationResult> ValidateGroundingAsync(ResponseValidationRequest request, ResponseValidationResult basicResult, CancellationToken cancellationToken)
    {
        if (request.Prompt.Sources.Count == 0) return Result(ValidationStatus.InsufficientKnowledge, SafeInsufficientAnswer, [], ErrorCodes.InsufficientKnowledge, false);
        var extraction = await claims.ExtractAsync(request.ModelResponse.Content, cancellationToken);
        if (!extraction.IsComplete) return Result(ValidationStatus.RequiresReview, "Não foi possível validar todas as afirmações.", basicResult.CitedSourceIds, extraction.ErrorCode ?? ErrorCodes.ValidationDependencyUnavailable, false);
        if (extraction.Claims.Count == 0) return basicResult;

        var allowed = request.Prompt.Sources.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cited = CitationPattern().Matches(request.ModelResponse.Content).Select(x => x.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (cited.Any(x => !allowed.Contains(x))) return Result(ValidationStatus.InvalidFormat, "Não foi possível validar as fontes da resposta.", [], ErrorCodes.InvalidCitation, true);

        SemanticGroundingResult semantic;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(policy.ExternalTimeoutMs);
            semantic = await grounding.EvaluateAsync(extraction.Claims, request.Prompt.Sources, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result(ValidationStatus.RequiresReview, "A verificação semântica está temporariamente indisponível.", cited, ErrorCodes.ValidationDependencyUnavailable, false) with { SemanticOutcome = SemanticCheckOutcome.Failed };
        }
        catch (Exception)
        {
            return Result(ValidationStatus.RequiresReview, "A verificação semântica está temporariamente indisponível.", cited, ErrorCodes.ValidationDependencyUnavailable, false) with { SemanticOutcome = SemanticCheckOutcome.Failed };
        }

        var supported = semantic.Claims.Count(x => x.Status == ClaimGroundingStatus.Supported);
        var material = Math.Max(1, semantic.Claims.Count);
        var components = new GroundingScoreComponents(
            request.Retrieval is null ? 1 : Math.Clamp((double)request.Retrieval.Items.Count / Math.Max(1, request.Prompt.Sources.Count), 0, 1),
            Math.Clamp((double)semantic.Claims.Count(x => x.Claim.CitationIds.Any(id => allowed.Contains(id))) / material, 0, 1),
            (double)supported / material,
            request.Intent?.Confidence ?? 1);
        var score = policy.Calculate(components);
        var contradicted = semantic.Claims.Any(x => x.Status == ClaimGroundingStatus.Contradicted);
        var status = supported == 0 ? ValidationStatus.InsufficientKnowledge : contradicted || supported < semantic.Claims.Count || score < policy.GroundedThreshold
            ? (score >= policy.PartiallyGroundedThreshold ? ValidationStatus.PartiallyGrounded : ValidationStatus.InsufficientKnowledge)
            : ValidationStatus.Grounded;
        var answer = status == ValidationStatus.InsufficientKnowledge ? SafeInsufficientAnswer : request.ModelResponse.Content;
        var reason = status == ValidationStatus.Grounded ? null : ErrorCodes.UnsupportedClaim;
        var result = reason is null ? new ResponseValidationResult(status, answer, cited, []) : Result(status, answer, cited, reason, status == ValidationStatus.PartiallyGrounded);
        return result with { Confidence = score, Claims = semantic.Claims, ScoreComponents = components, SemanticOutcome = semantic.Outcome, RegenerationRecommended = status == ValidationStatus.PartiallyGrounded };
    }

    private static ResponseValidationResult Result(ValidationStatus status, string answer, IReadOnlyList<string> cited, string code, bool correctable) =>
        new(status, answer, cited, [code]) { SanitizedReasons = [new(code, code, correctable)], RegenerationRecommended = correctable };
    private void SafeTelemetry(AdvancedValidationTelemetry value) { try { telemetry.RecordValidation(value); } catch { } }
    private static string ScoreBand(double score) => score >= .8 ? "high" : score >= .55 ? "medium" : "low";
    [GeneratedRegex(@"\[([a-zA-Z0-9][a-zA-Z0-9._:-]{0,199})\]")]
    private static partial Regex CitationPattern();
}
