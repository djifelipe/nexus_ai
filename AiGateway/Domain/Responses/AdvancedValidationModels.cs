namespace AiGateway.Domain.Responses;

public enum ClaimKind { Factual, Procedural, BusinessRule }
public enum ClaimGroundingStatus { Supported, Unsupported, Contradicted, Indeterminate }
public enum SensitiveDataCategory { Credential, Token, ConnectionString, InternalPrompt, Sql, StackTrace, Personal, Banking, Fiscal, CrossTenant, PermissionBypass }
public enum SemanticCheckOutcome { Completed, Degraded, Failed, NotRequired }

public sealed record VerifiableClaim(string Id, string Text, int Start, int Length, ClaimKind Kind, IReadOnlyList<string> CitationIds);
public sealed record ClaimEvidence(string SourceId, double Score, bool IsContradiction, string PolicyVersion);
public sealed record ClaimValidationResult(VerifiableClaim Claim, ClaimGroundingStatus Status, IReadOnlyList<ClaimEvidence> Evidence, string? ReasonCode);
public sealed record GroundingScoreComponents(double RetrievalCoverage, double CitationCoverage, double SemanticGrounding, double IntentConfidence)
{
    public GroundingScoreComponents Normalize() => new(Clamp(RetrievalCoverage), Clamp(CitationCoverage), Clamp(SemanticGrounding), Clamp(IntentConfidence));
    private static double Clamp(double value) => Math.Clamp(value, 0, 1);
}
public sealed record SanitizedValidationReason(string Code, string Category, bool Correctable);
public sealed record SensitiveDataFinding(SensitiveDataCategory Category, int Start, int Length, string Code);
public sealed record ClaimExtractionResult(IReadOnlyList<VerifiableClaim> Claims, bool IsComplete, string? ErrorCode = null);
public sealed record SemanticGroundingResult(IReadOnlyList<ClaimValidationResult> Claims, SemanticCheckOutcome Outcome, string? ErrorCode = null);
public sealed record AdvancedValidationTelemetry(
    string RequestId, string? ConversationId, ValidationStatus Status, string ScoreBand, string PolicyVersion,
    int ClaimCount, int SupportedClaimCount, int UnsupportedClaimCount, double CitationCoverage,
    SemanticCheckOutcome SemanticOutcome, int Attempt, bool Regenerated, string? TriggerCode, double DurationMs);
