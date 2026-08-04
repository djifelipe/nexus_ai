using System.Collections.Concurrent;
using System.Diagnostics;
using AiGateway.Domain;
using AiGateway.Domain.Policies;
using Microsoft.Extensions.Options;

namespace AiGateway.Application.Retrieval;

public sealed class HybridKnowledgeRetriever : IKnowledgeRetriever
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> SingleFlight = new(StringComparer.Ordinal);
    private readonly IKnowledgeRepository _repository;
    private readonly IEmbeddingClient _embeddings;
    private readonly ITokenEstimator _tokens;
    private readonly AiGatewayOptions _gateway;
    private readonly AdvancedRetrievalOptions _advanced;
    private readonly IRetrievalAccessScopeFactory _scopeFactory;
    private readonly IRetrievalAccessPolicy _accessPolicy;
    private readonly IGraphKnowledgeExpander? _graph;
    private readonly IRelationAllowlistPolicy _relations;
    private readonly ScoreFusionService _fusion;
    private readonly IRetrievalDeduplicationPolicy _deduplication;
    private readonly IRetrievalCache? _cache;
    private readonly ICacheKeyFactory _keys;
    private readonly IAiTelemetry? _telemetry;

    public HybridKnowledgeRetriever(IKnowledgeRepository repository, IEmbeddingClient embeddings, ITokenEstimator tokens, IOptions<AiGatewayOptions> options)
        : this(repository, embeddings, tokens, options, Options.Create(new AdvancedRetrievalOptions()),
            new RetrievalAccessPolicy(), new RetrievalAccessPolicy(), null,
            new RelationAllowlistPolicy(Options.Create(new AdvancedRetrievalOptions())),
            new ScoreFusionService(new RetrievalRankingPolicy(Options.Create(new AdvancedRetrievalOptions()))),
            new RetrievalDeduplicationPolicy(), null,
            new SecureCacheKeyFactory(Options.Create(new AdvancedRetrievalOptions())), null)
    { }

    public HybridKnowledgeRetriever(IKnowledgeRepository repository, IEmbeddingClient embeddings, ITokenEstimator tokens,
        IOptions<AiGatewayOptions> gateway, IOptions<AdvancedRetrievalOptions> advanced,
        IRetrievalAccessScopeFactory scopeFactory, IRetrievalAccessPolicy accessPolicy,
        IGraphKnowledgeExpander? graph, IRelationAllowlistPolicy relations, ScoreFusionService fusion,
        IRetrievalDeduplicationPolicy deduplication, IRetrievalCache? cache, ICacheKeyFactory keys, IAiTelemetry? telemetry)
    {
        _repository = repository; _embeddings = embeddings; _tokens = tokens; _gateway = gateway.Value; _advanced = advanced.Value;
        _scopeFactory = scopeFactory; _accessPolicy = accessPolicy; _graph = graph; _relations = relations; _fusion = fusion;
        _deduplication = deduplication; _cache = cache; _keys = keys; _telemetry = telemetry;
    }

    public async Task<RetrievalResult> RetrieveAsync(RetrievalRequest request, CancellationToken cancellationToken)
    {
        var access = _scopeFactory.Create(request);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_advanced.AdvancedRankingEnabled) deadline.CancelAfter(TimeSpan.FromMilliseconds(_advanced.RetrievalTimeoutMs));
        var ct = deadline.Token;
        var diagnostics = new List<StrategyDiagnostics>(); var warnings = new List<string>();
        var revision = await Timed("revision", () => _repository.GetKnowledgeRevisionAsync(access, ct), diagnostics);
        var fingerprint = _keys.CreateFingerprint(access, request, revision);
        var key = _keys.CreateSearchKey(fingerprint);

        if (_advanced.SearchCacheEnabled && _cache is not null)
        {
            var cached = await Timed("cache", () => _cache.GetAsync(key, fingerprint, ct), diagnostics);
            if (cached is not null) return cached.Result with { AccessScope = access, KnowledgeRevision = revision };
        }

        var gate = SingleFlight.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (_advanced.SearchCacheEnabled && _cache is not null)
            {
                var cached = await _cache.GetAsync(key, fingerprint, ct);
                if (cached is not null) return cached.Result with { AccessScope = access, KnowledgeRevision = revision };
            }
            return await RetrieveAndCache(request, access, revision, fingerprint, key, diagnostics, warnings, ct);
        }
        finally { gate.Release(); if (gate.CurrentCount == 1) SingleFlight.TryRemove(key, out _); }
    }

    private async Task<RetrievalResult> RetrieveAndCache(RetrievalRequest request, RetrievalAccessScope access, string revision,
        CacheScopeFingerprint fingerprint, string key, List<StrategyDiagnostics> diagnostics, List<string> warnings, CancellationToken ct)
    {
        var maxResults = Math.Min(request.MaxResults, _advanced.AdvancedRankingEnabled ? _advanced.MaxResults : _gateway.MaxResults);
        var maxTokens = Math.Min(request.MaxContextTokens, _advanced.AdvancedRankingEnabled ? _advanced.MaxContextTokens : _gateway.MaxContextTokens);
        using var sourceTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (_advanced.AdvancedRankingEnabled) sourceTimeout.CancelAfter(TimeSpan.FromMilliseconds(_advanced.SourceTimeoutMs));
        var sourceCt = sourceTimeout.Token;
        var structuredTask = Timed("sql", () => _repository.SearchStructuredAsync(request, sourceCt), diagnostics);
        var embedding = await Timed("embedding", () => _embeddings.CreateAsync(request.Question, sourceCt), diagnostics);
        var vectorTask = Timed("pgvector", () => _repository.SearchVectorAsync(request, embedding, sourceCt), diagnostics);
        await Task.WhenAll(structuredTask, vectorTask);
        var candidates = (await structuredTask).Concat(await vectorTask).Where(item => _accessPolicy.IsCandidateAuthorized(item, access)).ToList();

        GraphExpansionResult graphResult = new([], [], 0, 0, true);
        if (_advanced.GraphEnabled && _graph is not null && candidates.Count > 0)
        {
            try
            {
                using var graphTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct); graphTimeout.CancelAfter(TimeSpan.FromMilliseconds(_advanced.GraphTimeoutMs));
                var seeds = candidates.OrderByDescending(x => x.FinalScore).Take(Math.Min(10, _advanced.GraphMaxNodes)).Select(x => new GraphSeed(x.Id, x.Type, x.Id, x.FinalScore)).ToArray();
                graphResult = await Timed("graph", () => _graph.ExpandAsync(new(seeds, access, _relations.AllowedRelations, _advanced.GraphDepth, _advanced.GraphMaxNodes, _advanced.GraphMaxPaths), graphTimeout.Token), diagnostics);
                candidates.AddRange(graphResult.Items.Where(item => _accessPolicy.IsCandidateAuthorized(item, access)).Select(item => item with { GraphPaths = graphResult.Paths.Where(path => path.Nodes.Any(n => n.SourceId == item.Id)).ToArray() }));
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { warnings.Add("graph-timeout"); }
            catch (AiGatewayException ex) when (ex.Code == ErrorCodes.GraphUnavailable) { warnings.Add("graph-unavailable"); }
        }

        IReadOnlyList<DeduplicationGroup> groups = [];
        var phaseOne = candidates.GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Select(x => x.OrderByDescending(y => y.FinalScore).First()).OrderByDescending(x => x.IsCritical).ThenByDescending(x => x.FinalScore).ToArray();
        IReadOnlyList<KnowledgeItem> ranked = phaseOne;
        if (_advanced.AdvancedRankingEnabled)
        {
            var advancedRanked = _deduplication.Deduplicate(_fusion.Fuse(request.Intent.Type, candidates), _advanced.SemanticDeduplicationThreshold, out groups);
            if (_advanced.ShadowModeEnabled)
            {
                if (!phaseOne.Select(x => x.Id).SequenceEqual(advancedRanked.Select(x => x.Id), StringComparer.OrdinalIgnoreCase)) warnings.Add("shadow-ranking-difference");
            }
            else ranked = advancedRanked;
        }

        var selected = new List<KnowledgeItem>(); var usedTokens = 0;
        foreach (var item in ranked)
        {
            var cost = _tokens.Estimate(item.Title) + _tokens.Estimate(item.Content);
            if (selected.Count >= maxResults || usedTokens + cost > maxTokens) continue;
            selected.Add(item); usedTokens += cost;
        }
        var outcome = selected.Count == 0 ? RetrievalOutcome.Empty : warnings.Count > 0 ? RetrievalOutcome.Degraded : RetrievalOutcome.Success;
        var advancedDiagnostics = new AdvancedRetrievalDiagnostics(outcome, selected.FirstOrDefault()?.RankingPolicyVersion ?? "phase-1", diagnostics,
            _advanced.SearchCacheEnabled ? CacheOutcome.Miss : CacheOutcome.Bypass, Math.Min(candidates.Count, 10), graphResult.VisitedNodes,
            graphResult.Paths.Count, graphResult.MaximumDepth, groups.Sum(x => x.Suppressed.Count), groups, warnings);
        var result = new RetrievalResult(selected, new(["sql", "pgvector", .. (_advanced.GraphEnabled ? ["graph"] : Array.Empty<string>())],
            ["tenant", "erp-version", "permissions", "active", "published", "validity", "language", "content-type"], ranked.Count,
            ranked.Count > maxResults, ranked.Sum(x => _tokens.Estimate(x.Title) + _tokens.Estimate(x.Content)) > maxTokens)
        { Advanced = advancedDiagnostics })
        { AccessScope = access, KnowledgeRevision = revision };

        if (_advanced.SearchCacheEnabled && _cache is not null)
        {
            var now = DateTimeOffset.UtcNow; var ttl = TimeSpan.FromMinutes(_advanced.SearchCacheTtlMinutes);
            var metadata = new CacheEntryMetadata(fingerprint.Scope, fingerprint.Query, fingerprint.SchemaVersion, fingerprint.PolicyVersion, revision, access.ErpVersion, fingerprint.Permission, now, now + ttl);
            await _cache.SetAsync(key, new(metadata, result), ttl, ct);
        }
        return result;
    }

    private async Task<T> Timed<T>(string stage, Func<Task<T>> action, List<StrategyDiagnostics> diagnostics)
    {
        using var activity = _telemetry?.StartRetrievalStage(stage); var watch = Stopwatch.StartNew();
        try { var result = await action(); diagnostics.Add(new(stage, watch.ElapsedMilliseconds, result is System.Collections.ICollection c ? c.Count : 0)); return result; }
        catch { diagnostics.Add(new(stage, watch.ElapsedMilliseconds, 0, "failure")); throw; }
        finally { _telemetry?.RecordRetrievalEvent(stage, "completed", watch.Elapsed.TotalMilliseconds); }
    }
}
