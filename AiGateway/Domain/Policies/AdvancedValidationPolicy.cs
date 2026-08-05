using AiGateway.Domain.Responses;

namespace AiGateway.Domain.Policies;

public sealed record AdvancedValidationPolicy(
    double RetrievalWeight = .35,
    double CitationWeight = .25,
    double SemanticWeight = .25,
    double IntentWeight = .15,
    double GroundedThreshold = .80,
    double PartiallyGroundedThreshold = .55,
    double SemanticSupportThreshold = .55,
    double SemanticContradictionThreshold = .25,
    int MaxResponseCharacters = 20_000,
    int MaxClaims = 30,
    int MaxEvidenceCandidatesPerClaim = 5,
    int ExternalTimeoutMs = 1_500,
    string Version = "phase-4-v1")
{
    public static IReadOnlySet<SensitiveDataCategory> BlockingCategories { get; } = new HashSet<SensitiveDataCategory>
    {
        SensitiveDataCategory.Credential, SensitiveDataCategory.Token, SensitiveDataCategory.ConnectionString,
        SensitiveDataCategory.InternalPrompt, SensitiveDataCategory.Sql, SensitiveDataCategory.StackTrace,
        SensitiveDataCategory.CrossTenant, SensitiveDataCategory.PermissionBypass, SensitiveDataCategory.Personal,
        SensitiveDataCategory.Banking, SensitiveDataCategory.Fiscal
    };

    public double Calculate(GroundingScoreComponents components)
    {
        var value = components.Normalize();
        return Math.Clamp(value.RetrievalCoverage * RetrievalWeight + value.CitationCoverage * CitationWeight +
            value.SemanticGrounding * SemanticWeight + value.IntentConfidence * IntentWeight, 0, 1);
    }

    public bool CanRegenerate(ValidationStatus status, IReadOnlyList<SanitizedValidationReason> reasons) =>
        status is ValidationStatus.PartiallyGrounded or ValidationStatus.InvalidFormat && reasons.Any(x => x.Correctable);
}
