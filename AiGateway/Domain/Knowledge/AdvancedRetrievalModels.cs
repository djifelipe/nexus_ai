namespace AiGateway.Domain;

public sealed record RetrievalAccessScope(
    string TenantId,
    string ErpVersion,
    IReadOnlySet<string> EffectivePermissions,
    string Language,
    DateTimeOffset EffectiveAt,
    string RequestId,
    string TraceId);

public sealed record CacheScopeFingerprint(
    string Scope,
    string Permission,
    string Query,
    string Intent,
    string KnowledgeRevision,
    string SchemaVersion,
    string PolicyVersion);

public sealed record GraphSeed(string NodeId, string NodeType, string SourceId, double Score);
public sealed record GraphNode(string Id, string Type, string SourceId, string? Title = null);
public sealed record GraphEdge(string FromId, string ToId, string Relation);
public sealed record GraphPath(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges, int Depth, double Score);

public sealed record ScoreContribution(
    string Strategy,
    double RawScore,
    double NormalizedScore,
    double Weight,
    double WeightedScore);

public sealed record SuppressedSource(string SourceId, string Reason);
public sealed record DeduplicationGroup(string RetainedSourceId, IReadOnlyList<SuppressedSource> Suppressed);

public enum RetrievalOutcome
{
    Success,
    Empty,
    Degraded,
    DependencyFailure,
    AccessFailure,
    Cancelled
}

public enum CacheOutcome { Bypass, Hit, Miss, Stale, Invalid, Error, Stored, Coalesced }

public sealed record StrategyDiagnostics(
    string Strategy,
    long DurationMs,
    int CandidateCount,
    string? DegradedReason = null);

public sealed record AdvancedRetrievalDiagnostics(
    RetrievalOutcome Outcome,
    string RankingPolicyVersion,
    IReadOnlyList<StrategyDiagnostics> StrategyDetails,
    CacheOutcome SearchCacheOutcome,
    int GraphSeedCount,
    int GraphVisitedNodeCount,
    int GraphPathCount,
    int MaximumGraphDepth,
    int DeduplicatedCount,
    IReadOnlyList<DeduplicationGroup> DeduplicationGroups,
    IReadOnlyList<string> Warnings)
{
    public static AdvancedRetrievalDiagnostics PhaseOne { get; } = new(
        RetrievalOutcome.Success, "phase-1", [], CacheOutcome.Bypass, 0, 0, 0, 0, 0, [], []);
}

public sealed record CacheEntryMetadata(
    string ScopeFingerprint,
    string QueryFingerprint,
    string SchemaVersion,
    string PolicyVersion,
    string KnowledgeRevision,
    string ErpVersion,
    string PermissionFingerprint,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);

public sealed record RetrievalCacheEntry(CacheEntryMetadata Metadata, RetrievalResult Result);
public sealed record ResponseCacheEntry(CacheEntryMetadata Metadata, AiResponse Response, IReadOnlyList<string> SourceIds);

public sealed record ResponseCacheAdmissionRequest(
    ValidationStatus Status,
    bool ContainsSensitiveData,
    bool UsedTools,
    bool IsUserSpecific,
    IReadOnlyList<string> CitedSourceIds,
    IReadOnlySet<string> AuthorizedSourceIds);
