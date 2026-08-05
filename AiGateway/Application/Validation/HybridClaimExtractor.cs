using AiGateway.Domain.Responses;
using Microsoft.Extensions.Options;

namespace AiGateway.Application.Validation;

public sealed class HybridClaimExtractor(DeterministicClaimExtractor deterministic, IModelClaimExtractor model, IOptions<AdvancedValidationOptions> options) : IClaimExtractor
{
    public async Task<ClaimExtractionResult> ExtractAsync(string answer, CancellationToken cancellationToken)
    {
        var fallback = await deterministic.ExtractAsync(answer, cancellationToken);
        if (!options.Value.ModelClaimExtractionEnabled) return fallback;
        var enhanced = await model.ExtractStructuredAsync(answer, cancellationToken);
        return enhanced.IsComplete && enhanced.Claims.Count > 0 ? enhanced : fallback with { IsComplete = false, ErrorCode = enhanced.ErrorCode };
    }
}
