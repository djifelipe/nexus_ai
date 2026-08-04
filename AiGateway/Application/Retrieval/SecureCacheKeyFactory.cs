using System.Security.Cryptography;
using System.Text;
using AiGateway.Domain;
using Microsoft.Extensions.Options;

namespace AiGateway.Application.Retrieval;

public sealed class SecureCacheKeyFactory(IOptions<AdvancedRetrievalOptions> options) : ICacheKeyFactory
{
    public CacheScopeFingerprint CreateFingerprint(RetrievalAccessScope scope, RetrievalRequest request, string knowledgeRevision)
    {
        var permissions = string.Join('\n', scope.EffectivePermissions.Order(StringComparer.OrdinalIgnoreCase));
        var scopeValue = Digest($"tenant={scope.TenantId}\nversion={scope.ErpVersion}\nlanguage={scope.Language}");
        var permission = Digest(permissions);
        var query = Digest(Normalize(request.Question));
        var intent = Digest($"{request.Intent.Module}|{request.Intent.Feature}|{request.Intent.Action}|{request.Intent.Type}");
        return new(scopeValue, permission, query, intent, knowledgeRevision, options.Value.SchemaVersion, options.Value.RankingPolicyVersion);
    }

    public string CreateSearchKey(CacheScopeFingerprint value) => $"retrieval:{Digest(Canonical(value))}";
    public string CreateResponseKey(CacheScopeFingerprint value, string modelPolicyVersion) => $"response:{Digest(Canonical(value) + "|" + modelPolicyVersion)}";

    private string Digest(string value)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(options.Value.CacheKeySecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static string Canonical(CacheScopeFingerprint value) => string.Join('|', value.Scope, value.Permission, value.Query, value.Intent, value.KnowledgeRevision, value.SchemaVersion, value.PolicyVersion);
    private static string Normalize(string value) => string.Join(' ', value.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
