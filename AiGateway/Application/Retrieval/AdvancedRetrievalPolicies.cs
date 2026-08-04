using System.Globalization;
using AiGateway.Domain;
using AiGateway.Domain.Policies;
using Microsoft.Extensions.Options;

namespace AiGateway.Application.Retrieval;

public sealed class RetrievalAccessPolicy : IRetrievalAccessPolicy, IRetrievalAccessScopeFactory
{
    public RetrievalAccessScope Create(RetrievalRequest request, string? requestId = null, string? traceId = null)
        => Create(request.UserContext, requestId ?? "retrieval", traceId ?? "retrieval", DateTimeOffset.UtcNow);

    public RetrievalAccessScope Create(UserContext userContext, string requestId, string traceId, DateTimeOffset effectiveAt)
    {
        if (string.IsNullOrWhiteSpace(userContext.CompanyId) || string.IsNullOrWhiteSpace(userContext.ErpVersion) ||
            string.IsNullOrWhiteSpace(userContext.Language) || userContext.Permissions is null)
            throw new AiGatewayException(ErrorCodes.RetrievalAccessContextInvalid, "O contexto autenticado de recuperação está incompleto.");
        return new(userContext.CompanyId, userContext.ErpVersion,
            userContext.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase), userContext.Language,
            effectiveAt, requestId, traceId);
    }

    public bool IsCandidateAuthorized(KnowledgeItem item, RetrievalAccessScope scope)
    {
        var metadata = item.Metadata;
        if (metadata.TryGetValue("company_id", out var tenant) && !string.Equals(tenant, scope.TenantId, StringComparison.Ordinal)) return false;
        if (metadata.TryGetValue("erp_version", out var version) && !string.IsNullOrWhiteSpace(version) && !string.Equals(version, scope.ErpVersion, StringComparison.OrdinalIgnoreCase)) return false;
        if (metadata.TryGetValue("language", out var language) && !string.Equals(language, scope.Language, StringComparison.OrdinalIgnoreCase)) return false;
        if (metadata.TryGetValue("required_permission", out var permission) && !string.IsNullOrWhiteSpace(permission) && !scope.EffectivePermissions.Contains(permission)) return false;
        if (metadata.TryGetValue("publication_status", out var status) && !string.Equals(status, "published", StringComparison.OrdinalIgnoreCase)) return false;
        if (metadata.TryGetValue("is_active", out var active) && (!bool.TryParse(active, out var isActive) || !isActive)) return false;
        if (metadata.TryGetValue("valid_from", out var validFrom) && DateTimeOffset.TryParse(validFrom, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var from) && from > scope.EffectiveAt) return false;
        if (metadata.TryGetValue("valid_to", out var validTo) && DateTimeOffset.TryParse(validTo, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var to) && to <= scope.EffectiveAt) return false;
        return true;
    }
}

public sealed class RetrievalRankingPolicy(IOptions<AdvancedRetrievalOptions> options) : IRetrievalRankingPolicy
{
    public string Version => options.Value.RankingPolicyVersion;
    public RetrievalWeights For(IntentType intentType)
    {
        var value = intentType switch
        {
            IntentType.HowTo => options.Value.HowTo,
            IntentType.Explanation => options.Value.Explanation,
            IntentType.PermissionCheck => options.Value.PermissionCheck,
            IntentType.ImpactAnalysis => options.Value.ImpactAnalysis,
            _ => options.Value.Default
        };
        return new(value.Sql, value.Vector, value.Graph);
    }
}

public sealed class RelationAllowlistPolicy(IOptions<AdvancedRetrievalOptions> options) : IRelationAllowlistPolicy
{
    public IReadOnlySet<string> AllowedRelations { get; } = options.Value.AllowedGraphRelations.ToHashSet(StringComparer.OrdinalIgnoreCase);
    public bool IsAllowed(string relation) => AllowedRelations.Contains(relation);
}

public sealed class ResponseCacheAdmissionPolicy : ICacheAdmissionPolicy
{
    public bool CanCacheResponse(ResponseCacheAdmissionRequest request)
        => request.Status == ValidationStatus.Grounded && !request.ContainsSensitiveData && !request.UsedTools && !request.IsUserSpecific &&
           request.CitedSourceIds.Count > 0 && request.CitedSourceIds.All(request.AuthorizedSourceIds.Contains);
}

public sealed class RetrievalDeduplicationPolicy : IRetrievalDeduplicationPolicy
{
    public IReadOnlyList<KnowledgeItem> Deduplicate(IReadOnlyList<KnowledgeItem> candidates, double semanticThreshold, out IReadOnlyList<DeduplicationGroup> groups)
    {
        var retained = new List<KnowledgeItem>();
        var decisions = new List<DeduplicationGroup>();
        foreach (var candidate in candidates.OrderByDescending(x => x.IsCritical).ThenByDescending(x => x.FinalScore).ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            var duplicate = retained.FirstOrDefault(existing => IsDuplicate(existing, candidate, semanticThreshold));
            if (duplicate is null) { retained.Add(candidate); continue; }
            var reason = string.Equals(duplicate.Id, candidate.Id, StringComparison.OrdinalIgnoreCase) ? "exact-source" :
                SameLogicalSource(duplicate, candidate) ? "obsolete-or-redundant-version" : "semantic-equivalent";
            var suppressed = new SuppressedSource(candidate.Id, reason);
            var index = retained.IndexOf(duplicate);
            retained[index] = duplicate with { SuppressedSources = duplicate.SuppressedSources.Append(suppressed).ToArray() };
            decisions.Add(new(duplicate.Id, [suppressed]));
        }
        groups = decisions;
        return retained;
    }

    private static bool IsDuplicate(KnowledgeItem left, KnowledgeItem right, double threshold)
    {
        if (string.Equals(left.Id, right.Id, StringComparison.OrdinalIgnoreCase)) return true;
        if (left.IsCritical || right.IsCritical) return false;
        if (SameLogicalSource(left, right)) return true;
        var similarity = TokenSimilarity(left.Content, right.Content);
        return similarity >= threshold && SameApplicability(left, right);
    }

    private static bool SameLogicalSource(KnowledgeItem left, KnowledgeItem right)
        => left.Metadata.TryGetValue("logical_source_id", out var l) && right.Metadata.TryGetValue("logical_source_id", out var r) && string.Equals(l, r, StringComparison.OrdinalIgnoreCase);

    private static bool SameApplicability(KnowledgeItem left, KnowledgeItem right)
        => string.Equals(left.Module, right.Module, StringComparison.OrdinalIgnoreCase) && string.Equals(left.Feature, right.Feature, StringComparison.OrdinalIgnoreCase);

    private static double TokenSimilarity(string left, string right)
    {
        var a = Tokens(left); var b = Tokens(right);
        if (a.Count == 0 && b.Count == 0) return 1;
        return a.Intersect(b, StringComparer.OrdinalIgnoreCase).Count() / (double)a.Union(b, StringComparer.OrdinalIgnoreCase).Count();
    }

    private static HashSet<string> Tokens(string value) => value.Split([' ', '\r', '\n', '\t', '.', ',', ';', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
