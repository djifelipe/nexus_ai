using System.Text.Json;
using AiGateway.Application;
using AiGateway.Domain;
using Microsoft.Extensions.Caching.Distributed;

namespace AiGateway.Infrastructure.Redis;

public sealed class DistributedRetrievalCache(IDistributedCache cache, ILogger<DistributedRetrievalCache> logger) : IRetrievalCache
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<RetrievalCacheEntry?> GetAsync(string key, CacheScopeFingerprint expected, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await cache.GetAsync(key, cancellationToken);
            if (bytes is null) return null;
            var entry = JsonSerializer.Deserialize<RetrievalCacheEntry>(bytes, Json);
            if (entry is null || !Valid(entry.Metadata, expected)) { await cache.RemoveAsync(key, cancellationToken); return null; }
            return entry;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning("Retrieval cache read failed: {ExceptionType}", ex.GetType().Name); return null; }
    }

    public async Task SetAsync(string key, RetrievalCacheEntry entry, TimeSpan ttl, CancellationToken cancellationToken)
    {
        try { await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(entry, Json), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning("Retrieval cache write failed: {ExceptionType}", ex.GetType().Name); }
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken) => cache.RemoveAsync(key, cancellationToken);

    private static bool Valid(CacheEntryMetadata metadata, CacheScopeFingerprint expected)
        => metadata.ExpiresAt > DateTimeOffset.UtcNow && metadata.ScopeFingerprint == expected.Scope && metadata.QueryFingerprint == expected.Query &&
           metadata.PermissionFingerprint == expected.Permission && metadata.ErpVersion.Length > 0 && metadata.KnowledgeRevision == expected.KnowledgeRevision &&
           metadata.SchemaVersion == expected.SchemaVersion && metadata.PolicyVersion == expected.PolicyVersion;
}

public sealed class DistributedResponseCache(IDistributedCache cache, ILogger<DistributedResponseCache> logger) : IResponseCache
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<ResponseCacheEntry?> GetAsync(string key, CacheScopeFingerprint expected, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await cache.GetAsync(key, cancellationToken); if (bytes is null) return null;
            var entry = JsonSerializer.Deserialize<ResponseCacheEntry>(bytes, Json);
            if (entry is null || entry.Metadata.ExpiresAt <= DateTimeOffset.UtcNow || entry.Metadata.ScopeFingerprint != expected.Scope || entry.Metadata.QueryFingerprint != expected.Query ||
                entry.Metadata.PermissionFingerprint != expected.Permission || entry.Metadata.KnowledgeRevision != expected.KnowledgeRevision || entry.Metadata.SchemaVersion != expected.SchemaVersion || entry.Metadata.PolicyVersion != expected.PolicyVersion)
            { await cache.RemoveAsync(key, cancellationToken); return null; }
            return entry;
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning("Response cache read failed: {ExceptionType}", ex.GetType().Name); return null; }
    }
    public async Task SetAsync(string key, ResponseCacheEntry entry, TimeSpan ttl, CancellationToken cancellationToken)
    { try { await cache.SetAsync(key, JsonSerializer.SerializeToUtf8Bytes(entry, Json), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, cancellationToken); } catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning("Response cache write failed: {ExceptionType}", ex.GetType().Name); } }
    public Task RemoveAsync(string key, CancellationToken cancellationToken) => cache.RemoveAsync(key, cancellationToken);
}
