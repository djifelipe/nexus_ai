using AiGateway.Application;
using AiGateway.Application.Retrieval;
using AiGateway.Domain;
using AiGateway.Domain.Policies;
using AiGateway.Infrastructure.Graph;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace AiGateway.Tests;

public sealed class AdvancedRetrievalTests
{
    [Theory]
    [InlineData(IntentType.HowTo,.45,.35,.20)]
    [InlineData(IntentType.Explanation,.20,.50,.30)]
    [InlineData(IntentType.PermissionCheck,.60,.05,.35)]
    [InlineData(IntentType.ImpactAnalysis,.25,.20,.55)]
    public void Ranking_policy_uses_specified_weights(IntentType intent,double sql,double vector,double graph)
    {
        var policy=new RetrievalRankingPolicy(Options.Create(Advanced()));var weights=policy.For(intent);
        Assert.Equal(sql,weights.Sql,5);Assert.Equal(vector,weights.Vector,5);Assert.Equal(graph,weights.Graph,5);Assert.Equal(1,weights.Sum,5);
    }

    [Fact]
    public void Fusion_preserves_contributions_and_missing_channels_are_zero()
    {
        var service=new ScoreFusionService(new RetrievalRankingPolicy(Options.Create(Advanced())));
        var result=Assert.Single(service.Fuse(IntentType.HowTo,[Item("a",sql:1,vector:.5,graph:0)]));
        Assert.Equal(.625,result.FinalScore,5);Assert.Equal(3,result.ScoreContributions.Count);Assert.Equal(0,result.ScoreContributions.Single(x=>x.Strategy=="graph").WeightedScore);
    }

    [Fact]
    public void Deduplication_preserves_critical_rules_and_tracks_suppression()
    {
        var policy=new RetrievalDeduplicationPolicy();
        var first=Item("a",content:"mesmo texto de regra",critical:true);var second=Item("b",content:"mesmo texto de regra",critical:true);
        var duplicate=Item("a",content:"mesmo texto de regra");
        var result=policy.Deduplicate([first,second,duplicate],.8,out var groups);
        Assert.Equal(2,result.Count);Assert.Contains(result,x=>x.Id=="a");Assert.Contains(result,x=>x.Id=="b");Assert.Single(groups);
    }

    [Fact]
    public void Options_validation_rejects_unsafe_cache_and_invalid_weights()
    {
        var options=WithOptions(Advanced(),new RetrievalWeightOptions(.8,.8,.1),searchCache:true);
        var result=new AdvancedRetrievalOptionsValidator().Validate(null,options);
        Assert.True(result.Failed);Assert.Contains(result.Failures!,x=>x.Contains("somar 1"));Assert.Contains(result.Failures!,x=>x.Contains("CacheKeySecret"));
    }

    [Fact]
    public void Cache_keys_are_opaque_and_isolated_by_tenant_permission_and_version()
    {
        var factory=new SecureCacheKeyFactory(Options.Create(Advanced(secret:"a-long-production-test-secret")));
        var request=Request();var a=new RetrievalAccessScope("tenant-a","1",new HashSet<string>{"secret.permission"},"pt-BR",DateTimeOffset.UtcNow,"r","t");
        var b=a with{TenantId="tenant-b"};var c=a with{EffectivePermissions=new HashSet<string>{"other"}};var d=a with{ErpVersion="2"};
        var keys=new[]{a,b,c,d}.Select(x=>factory.CreateSearchKey(factory.CreateFingerprint(x,request,"rev"))).ToArray();
        Assert.Equal(4,keys.Distinct().Count());Assert.All(keys,key=>{Assert.DoesNotContain("tenant",key);Assert.DoesNotContain("permission",key);Assert.DoesNotContain("cancelar",key);});
    }

    [Fact]
    public void Response_cache_admission_rejects_unsafe_partial_tool_and_unknown_sources()
    {
        var policy=new ResponseCacheAdmissionPolicy();var authorized=new HashSet<string>{"s1"};
        Assert.True(policy.CanCacheResponse(new(ValidationStatus.Grounded,false,false,false,["s1"],authorized)));
        Assert.False(policy.CanCacheResponse(new(ValidationStatus.PartiallyGrounded,false,false,false,["s1"],authorized)));
        Assert.False(policy.CanCacheResponse(new(ValidationStatus.Grounded,true,false,false,["s1"],authorized)));
        Assert.False(policy.CanCacheResponse(new(ValidationStatus.Grounded,false,true,false,["s1"],authorized)));
        Assert.False(policy.CanCacheResponse(new(ValidationStatus.Grounded,false,false,false,["other"],authorized)));
    }

    [Fact]
    public async Task Graph_expander_enforces_depth_relation_and_filter_integrity()
    {
        var allowed=new HashSet<string>{"HAS_RULE"};var repository=new GraphRepository(filtersVerified:true);
        var expander=new McpGraphKnowledgeExpander(repository);var scope=Scope();
        var result=await expander.ExpandAsync(new([new("seed","feature","seed",1)],scope,allowed,2,10,10),default);
        Assert.Single(result.Paths);Assert.Equal("HAS_RULE",result.Paths[0].Edges[0].Relation);Assert.Equal(1,result.MaximumDepth);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(()=>expander.ExpandAsync(new([new("seed","feature","seed",1)],scope,allowed,5,10,10),default));
        await Assert.ThrowsAsync<AiGatewayException>(()=>new McpGraphKnowledgeExpander(new GraphRepository(false)).ExpandAsync(new([new("seed","feature","seed",1)],scope,allowed,2,10,10),default));
    }

    [Fact]
    public async Task Advanced_pipeline_filters_candidates_fuses_deduplicates_and_meets_budget()
    {
        var options=WithOptions(Advanced(),advanced:true);var access=new RetrievalAccessPolicy();
        var repo=new StaticRepository([
            Item("allowed",sql:1,metadata:Metadata("company","1","p")),
            Item("other-tenant",vector:1,metadata:Metadata("other","1","p")),
            Item("wrong-version",vector:1,metadata:Metadata("company","2","p"))]);
        var retriever=new HybridKnowledgeRetriever(repo,new Embedding(),new Tokens(),Options.Create(new AiGatewayOptions()),Options.Create(options),access,access,null,
            new RelationAllowlistPolicy(Options.Create(options)),new ScoreFusionService(new RetrievalRankingPolicy(Options.Create(options))),new RetrievalDeduplicationPolicy(),null,
            new SecureCacheKeyFactory(Options.Create(options)),null);
        var result=await retriever.RetrieveAsync(Request(),default);
        Assert.Single(result.Items);Assert.Equal("allowed",result.Items[0].Id);Assert.Equal("phase-2-v1",result.Items[0].RankingPolicyVersion);Assert.Contains("content-type",result.Diagnostics.AppliedFilters);
        Assert.Equal(RetrievalOutcome.Success,result.Diagnostics.Advanced.Outcome);Assert.True(result.Diagnostics.Advanced.MaximumGraphDepth<=4);
    }

    [Fact]
    public async Task Search_cache_hits_only_for_the_same_access_scope_and_revision()
    {
        var options=WithOptions(Advanced("a-long-production-test-secret"),searchCache:true);var access=new RetrievalAccessPolicy();
        var repo=new CountingRepository([Item("allowed",sql:1,metadata:Metadata("company","1","p"))]);var cache=new MemoryRetrievalCache();
        var retriever=Retriever(repo,options,access,cache);
        var first=await retriever.RetrieveAsync(Request(),default);var second=await retriever.RetrieveAsync(Request(),default);
        Assert.Single(first.Items);Assert.Single(second.Items);Assert.Equal(1,repo.StructuredCalls);Assert.Equal(1,repo.VectorCalls);
        var other=Request() with{UserContext=Request().UserContext with{CompanyId="other"}};
        await retriever.RetrieveAsync(other,default);Assert.Equal(2,repo.StructuredCalls);
    }

    [Fact]
    public async Task Graph_timeout_degrades_without_returning_unverified_graph_data()
    {
        var options=new AdvancedRetrievalOptions{AdvancedRankingEnabled=true,GraphEnabled=true,GraphTimeoutMs=25,SourceTimeoutMs=450,ProcessingTimeoutMs=100,RetrievalTimeoutMs=800};var access=new RetrievalAccessPolicy();
        var repo=new StaticRepository([Item("allowed",sql:1,metadata:Metadata("company","1","p"))]);
        var retriever=new HybridKnowledgeRetriever(repo,new Embedding(),new Tokens(),Options.Create(new AiGatewayOptions()),Options.Create(options),access,access,new DelayedGraph(),
            new RelationAllowlistPolicy(Options.Create(options)),new ScoreFusionService(new RetrievalRankingPolicy(Options.Create(options))),new RetrievalDeduplicationPolicy(),null,new SecureCacheKeyFactory(Options.Create(options)),null);
        var result=await retriever.RetrieveAsync(Request(),default);
        Assert.Single(result.Items);Assert.Equal(RetrievalOutcome.Degraded,result.Diagnostics.Advanced.Outcome);Assert.Contains("graph-timeout",result.Diagnostics.Advanced.Warnings);
    }

    [Fact]
    public async Task Advanced_retrieval_meets_800ms_and_default_budget_acceptance_targets()
    {
        var options=Advanced();Assert.Equal(2,options.GraphDepth);Assert.True(options.GraphDepth<=4);Assert.Equal(15,options.MaxResults);Assert.Equal(8000,options.MaxContextTokens);
        var access=new RetrievalAccessPolicy();var values=Enumerable.Range(1,20).Select(i=>Item($"s{i}",sql:1-i/100d,content:$"procedimento exclusivo numero {i}",metadata:Metadata("company","1","p"))).ToArray();
        var retriever=Retriever(new StaticRepository(values),options,access);var watch=Stopwatch.StartNew();var result=await retriever.RetrieveAsync(Request(),default);
        Assert.True(watch.ElapsedMilliseconds<800,$"Advanced retrieval took {watch.ElapsedMilliseconds} ms.");Assert.Equal(15,result.Items.Count);Assert.True(result.Diagnostics.ResultLimitApplied);
    }

    [Fact]
    public void Mcp_contract_contains_all_mandatory_filters_and_no_direct_connection_type()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..",".."));
        var text=File.ReadAllText(Path.Combine(root,"Infrastructure","Mcp","KnowledgeBaseMcpClient.cs"));
        foreach(var expected in new[]{"company_id","erp_version","required_permission","is_active","publication_status","valid_from","valid_to","language","source_type"})Assert.Contains(expected,text);
        Assert.DoesNotContain("NpgsqlConnection",text);Assert.DoesNotContain("SqlConnection",text);
    }

    [Fact]
    public void Diagnostics_and_cache_contracts_do_not_expose_payload_fields()
    {
        var properties=typeof(AdvancedRetrievalDiagnostics).GetProperties().Select(x=>x.Name).Concat(typeof(CacheScopeFingerprint).GetProperties().Select(x=>x.Name)).ToArray();
        Assert.DoesNotContain(properties,x=>x.Contains("Question",StringComparison.OrdinalIgnoreCase)||x.Contains("Content",StringComparison.OrdinalIgnoreCase)||x.Contains("Credential",StringComparison.OrdinalIgnoreCase));
    }

    private static AdvancedRetrievalOptions Advanced(string secret="development-only-change-me")=>new(){AdvancedRankingEnabled=true,CacheKeySecret=secret};
    private static AdvancedRetrievalOptions WithOptions(AdvancedRetrievalOptions value,RetrievalWeightOptions? howTo=null,bool searchCache=false,bool advanced=true)=>new(){AdvancedRankingEnabled=advanced,SearchCacheEnabled=searchCache,CacheKeySecret=value.CacheKeySecret,HowTo=howTo??value.HowTo};
    private static RetrievalRequest Request()=>new("Como cancelar uma NF-e?",new("Fiscal","NFe","Cancel","Entity",IntentType.HowTo,.9,[],[],false,null,"test",["Fiscal"]),new("company","user","1","pt-BR",new HashSet<string>{"p"},new("Fiscal",null,null)),15,8000);
    private static RetrievalAccessScope Scope()=>new("company","1",new HashSet<string>{"p"},"pt-BR",DateTimeOffset.UtcNow,"r","t");
    private static IReadOnlyDictionary<string,string> Metadata(string tenant,string version,string permission)=>new Dictionary<string,string>{{"company_id",tenant},{"erp_version",version},{"required_permission",permission},{"publication_status","published"},{"is_active","true"},{"language","pt-BR"}};
    private static KnowledgeItem Item(string id,double sql=0,double vector=0,double graph=0,string content="conteudo autorizado",bool critical=false,IReadOnlyDictionary<string,string>? metadata=null)=>new(id,"workflow",id,content,"Fiscal","NFe","1",vector,sql,Math.Max(sql,vector),critical,metadata??new Dictionary<string,string>()){GraphScore=graph};
    private static HybridKnowledgeRetriever Retriever(IKnowledgeRepository repository,AdvancedRetrievalOptions options,RetrievalAccessPolicy access,IRetrievalCache? cache=null)=>new(repository,new Embedding(),new Tokens(),Options.Create(new AiGatewayOptions()),Options.Create(options),access,access,null,new RelationAllowlistPolicy(Options.Create(options)),new ScoreFusionService(new RetrievalRankingPolicy(Options.Create(options))),new RetrievalDeduplicationPolicy(),cache,new SecureCacheKeyFactory(Options.Create(options)),null);
    private sealed class Embedding:IEmbeddingClient{public int Dimensions=>3;public Task<ReadOnlyMemory<float>> CreateAsync(string input,CancellationToken cancellationToken)=>Task.FromResult<ReadOnlyMemory<float>>(new float[]{1,2,3});}
    private sealed class Tokens:ITokenEstimator{public int Estimate(string text)=>Math.Max(1,text.Length/4);}
    private sealed class StaticRepository(IReadOnlyList<KnowledgeItem> values):IKnowledgeRepository
    {public Task<IReadOnlyList<KnowledgeItem>> SearchStructuredAsync(RetrievalRequest request,CancellationToken cancellationToken)=>Task.FromResult(values);public Task<IReadOnlyList<KnowledgeItem>> SearchVectorAsync(RetrievalRequest request,ReadOnlyMemory<float> embedding,CancellationToken cancellationToken)=>Task.FromResult(values);public Task<string> GetKnowledgeRevisionAsync(RetrievalAccessScope scope,CancellationToken cancellationToken)=>Task.FromResult("rev-1");}
    private sealed class GraphRepository(bool filtersVerified):IKnowledgeRepository
    {
        public Task<IReadOnlyList<KnowledgeItem>> SearchStructuredAsync(RetrievalRequest request,CancellationToken cancellationToken)=>Task.FromResult<IReadOnlyList<KnowledgeItem>>([]);
        public Task<IReadOnlyList<KnowledgeItem>> SearchVectorAsync(RetrievalRequest request,ReadOnlyMemory<float> embedding,CancellationToken cancellationToken)=>Task.FromResult<IReadOnlyList<KnowledgeItem>>([]);
        public Task<GraphExpansionResult> ExpandGraphAsync(GraphExpansionRequest request,CancellationToken cancellationToken)
        {var good=new GraphPath([new("seed","feature","seed"),new("rule","rule","rule")],[new("seed","rule","HAS_RULE")],1,.8);var blocked=new GraphPath([new("seed","feature","seed"),new("x","x","x")],[new("seed","x","NOT_ALLOWED")],2,.9);return Task.FromResult(new GraphExpansionResult([], [good,blocked],2,2,filtersVerified));}
    }
    private sealed class CountingRepository(IReadOnlyList<KnowledgeItem> values):IKnowledgeRepository
    {public int StructuredCalls;public int VectorCalls;public Task<IReadOnlyList<KnowledgeItem>> SearchStructuredAsync(RetrievalRequest request,CancellationToken cancellationToken){StructuredCalls++;return Task.FromResult(values);}public Task<IReadOnlyList<KnowledgeItem>> SearchVectorAsync(RetrievalRequest request,ReadOnlyMemory<float> embedding,CancellationToken cancellationToken){VectorCalls++;return Task.FromResult(values);}public Task<string> GetKnowledgeRevisionAsync(RetrievalAccessScope scope,CancellationToken cancellationToken)=>Task.FromResult("rev-1");}
    private sealed class MemoryRetrievalCache:IRetrievalCache
    {private readonly Dictionary<string,RetrievalCacheEntry> _entries=[];public Task<RetrievalCacheEntry?> GetAsync(string key,CacheScopeFingerprint expected,CancellationToken cancellationToken)=>Task.FromResult(_entries.TryGetValue(key,out var value)?value:null);public Task SetAsync(string key,RetrievalCacheEntry entry,TimeSpan ttl,CancellationToken cancellationToken){_entries[key]=entry;return Task.CompletedTask;}public Task RemoveAsync(string key,CancellationToken cancellationToken){_entries.Remove(key);return Task.CompletedTask;}}
    private sealed class DelayedGraph:IGraphKnowledgeExpander
    {public async Task<GraphExpansionResult> ExpandAsync(GraphExpansionRequest request,CancellationToken cancellationToken){await Task.Delay(500,cancellationToken);return new([],[],0,0,true);}}
}
