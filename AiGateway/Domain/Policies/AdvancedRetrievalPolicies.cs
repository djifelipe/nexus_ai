namespace AiGateway.Domain.Policies;

using AiGateway.Domain;

public sealed record RetrievalWeights(double Sql, double Vector, double Graph)
{
    public double Sum => Sql + Vector + Graph;
}

public interface IRetrievalRankingPolicy
{
    string Version { get; }
    RetrievalWeights For(IntentType intentType);
}

public interface IRelationAllowlistPolicy
{
    bool IsAllowed(string relation);
    IReadOnlySet<string> AllowedRelations { get; }
}

public interface ICacheAdmissionPolicy
{
    bool CanCacheResponse(ResponseCacheAdmissionRequest request);
}

public interface IRetrievalAccessPolicy
{
    RetrievalAccessScope Create(UserContext userContext, string requestId, string traceId, DateTimeOffset effectiveAt);
    bool IsCandidateAuthorized(KnowledgeItem item, RetrievalAccessScope scope);
}

public interface IRetrievalDeduplicationPolicy
{
    IReadOnlyList<KnowledgeItem> Deduplicate(IReadOnlyList<KnowledgeItem> candidates, double semanticThreshold, out IReadOnlyList<DeduplicationGroup> groups);
}
