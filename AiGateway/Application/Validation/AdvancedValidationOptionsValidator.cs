using Microsoft.Extensions.Options;

namespace AiGateway.Application.Validation;

public sealed class AdvancedValidationOptionsValidator : IValidateOptions<AdvancedValidationOptions>
{
    public ValidateOptionsResult Validate(string? name, AdvancedValidationOptions value)
    {
        var sum = value.RetrievalWeight + value.CitationWeight + value.SemanticWeight + value.IntentWeight;
        if (Math.Abs(sum - 1) > .0001) return ValidateOptionsResult.Fail("Os pesos de validação avançada devem somar 1.");
        if (value.PartiallyGroundedThreshold >= value.GroundedThreshold) return ValidateOptionsResult.Fail("O limiar parcial deve ser menor que o limiar grounded.");
        if (value.SemanticContradictionThreshold >= value.SemanticSupportThreshold) return ValidateOptionsResult.Fail("O limiar de contradição deve ser menor que o de suporte.");
        return ValidateOptionsResult.Success;
    }
}
