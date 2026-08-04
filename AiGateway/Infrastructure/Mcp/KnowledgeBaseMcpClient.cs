using System.Globalization;
using System.Text.Json;
using AiGateway.Application;
using AiGateway.Domain;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AiGateway.Infrastructure.Mcp;

public sealed class KnowledgeBaseMcpClient(IOptions<KnowledgeBaseMcpOptions> options, ILogger<KnowledgeBaseMcpClient> logger) : IKnowledgeBaseMcpClient, IAsyncDisposable
{
    private readonly KnowledgeBaseMcpOptions _options = options.Value;
    private readonly object _clientLock = new();
    private Task<McpClient>? _client;

    public async Task<bool> CheckHealthAsync(CancellationToken ct)
    {
        try { await ExecuteAsync("select 1 as healthy", ct); return true; } catch { return false; }
    }

    public async Task<IReadOnlyList<IntentCatalogEntry>> GetActiveAsync(string companyId, IReadOnlySet<string> permissions, CancellationToken ct)
    {
        var sql = $"SELECT t.module_id,t.feature_id,t.action_id,t.entity_id,a.intent_type,array_agg(t.term) AS terms,max(t.weight) AS weight,t.required_permission FROM knowledge_intent_term t LEFT JOIN knowledge_action a ON a.id=t.action_id AND a.is_active JOIN knowledge_module m ON m.id=t.module_id AND m.is_active LEFT JOIN knowledge_feature f ON f.id=t.feature_id AND f.module_id=t.module_id AND f.is_active WHERE t.is_active AND (t.required_permission IS NULL OR t.required_permission=ANY({SqlArray(permissions)})) GROUP BY t.module_id,t.feature_id,t.action_id,t.entity_id,a.intent_type,t.required_permission";
        var rows = await ExecuteAsync(sql, ct);
        return rows.Select(row => new IntentCatalogEntry(RequiredString(row,"module_id"),OptionalString(row,"feature_id"),OptionalString(row,"action_id"),OptionalString(row,"entity_id"),Enum.TryParse<IntentType>(OptionalString(row,"intent_type"),true,out var type)?type:IntentType.Unknown,StringArray(row,"terms"),Double(row,"weight"),OptionalString(row,"required_permission"))).ToArray();
    }

    public Task<IReadOnlyList<KnowledgeItem>> SearchStructuredAsync(RetrievalRequest request, CancellationToken ct)
        => ReadKnowledgeAsync(CommonSelect(request,"0::double precision AS vector_score,CASE WHEN s.feature_id="+Sql(request.Intent.Feature)+" THEN 1.0 WHEN s.module_id="+Sql(request.Intent.Module)+" THEN 0.75 ELSE 0.5 END AS sql_score","s.is_critical DESC,sql_score DESC"),false,ct);

    public Task<IReadOnlyList<KnowledgeItem>> SearchVectorAsync(RetrievalRequest request, ReadOnlyMemory<float> embedding, CancellationToken ct)
    {
        if (embedding.Length != 768) throw new AiGatewayException(ErrorCodes.EmbeddingUnavailable,"A dimensão do embedding é incompatível com a base de conhecimento.");
        var literal=Sql("["+string.Join(',',embedding.ToArray().Select(value=>value.ToString(CultureInfo.InvariantCulture)))+"]");
        return ReadKnowledgeAsync(CommonSelect(request,$"1-(c.embedding<=>{literal}::vector) AS vector_score,0::double precision AS sql_score",$"c.embedding<=>{literal}::vector",true),true,ct);
    }

    private string CommonSelect(RetrievalRequest request,string scores,string order,bool vector=false)
    {
        var content=vector?"c.content":"s.content"; var from=vector?"knowledge_chunk c JOIN knowledge_source s ON s.id=c.source_id":"knowledge_source s"; var active=vector?"c.is_active AND ":"";
        return $"SELECT s.id,s.source_type,s.title,{content} AS content,s.module_id,s.feature_id,s.version,{scores},s.is_critical,s.metadata FROM {from} WHERE {active}s.company_id={Sql(request.UserContext.CompanyId)} AND (s.erp_version IS NULL OR s.erp_version={Sql(request.UserContext.ErpVersion)}) AND (s.required_permission IS NULL OR s.required_permission=ANY({SqlArray(request.UserContext.Permissions)})) AND s.is_active AND s.publication_status='published' AND (s.valid_from IS NULL OR s.valid_from<=now()) AND (s.valid_to IS NULL OR s.valid_to>now()) AND s.language={Sql(request.UserContext.Language)} AND ({Sql(request.Intent.Module)} IS NULL OR s.module_id={Sql(request.Intent.Module)}) ORDER BY {order} LIMIT {Math.Clamp(request.MaxResults,1,15)}";
    }

    private async Task<IReadOnlyList<KnowledgeItem>> ReadKnowledgeAsync(string sql,bool vector,CancellationToken ct)
    {
        var rows=await ExecuteAsync(sql,ct);
        return rows.Select(row=>new KnowledgeItem(RequiredString(row,"id"),RequiredString(row,"source_type"),RequiredString(row,"title"),RequiredString(row,"content"),OptionalString(row,"module_id"),OptionalString(row,"feature_id"),OptionalString(row,"version"),Double(row,"vector_score"),Double(row,"sql_score"),vector?Double(row,"vector_score"):Double(row,"sql_score"),Boolean(row,"is_critical"),StringDictionary(row,"metadata"))).ToArray();
    }

    private async Task<JsonElement[]> ExecuteAsync(string sql,CancellationToken cancellationToken)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        try
        {
            var client=await GetClientAsync();
            var result=await client.CallToolAsync(_options.QueryTool,new Dictionary<string,object?>{{"query",sql}},cancellationToken:timeout.Token);
            var text=result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text??"[]";
            try{using var envelope=JsonDocument.Parse(text);if(envelope.RootElement.ValueKind==JsonValueKind.Object&&envelope.RootElement.TryGetProperty("result",out var value)&&value.ValueKind==JsonValueKind.String)text=value.GetString()??"[]";}catch(JsonException){}
            var start=text.IndexOf('[');var end=text.IndexOf(']',start+1);while(end>=0&&end+1<text.Length&&text[(end+1)..].Contains(']'))end=text.IndexOf(']',end+1);
            if(start<0||end<start)return[];using var document=JsonDocument.Parse(text[start..(end+1)]);return document.RootElement.EnumerateArray().Select(element=>element.Clone()).ToArray();
        }
        catch(OperationCanceledException ex) when(!cancellationToken.IsCancellationRequested){throw new AiGatewayException(ErrorCodes.DatabaseUnavailable,"O MCP da base de conhecimento excedeu o tempo limite.",ex);}
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested){throw;}
        catch(AiGatewayException){throw;}
        catch(Exception ex){logger.LogWarning("MCP KB failure type {ExceptionType}, inner type {InnerExceptionType}",ex.GetType().Name,ex.InnerException?.GetType().Name??"none");lock(_clientLock){_client=null;}throw new AiGatewayException(ErrorCodes.DatabaseUnavailable,"O MCP da base de conhecimento está temporariamente indisponível.",ex);}
    }

    private Task<McpClient> GetClientAsync()
    {
        lock(_clientLock)return _client??=CreateClientAsync();
    }

    private Task<McpClient> CreateClientAsync()
    {
        IClientTransport transport;
            if(_options.Transport.Equals("Stdio",StringComparison.OrdinalIgnoreCase))
            {
                var environment=StdioClientTransportOptions.GetDefaultEnvironmentVariables();
                var credential=Environment.GetEnvironmentVariable(_options.CredentialEnvironmentVariable);
                if(!string.IsNullOrWhiteSpace(credential))environment[_options.CredentialEnvironmentVariable]=credential;
                transport=new StdioClientTransport(new StdioClientTransportOptions{Name=_options.ServerName,Command=_options.Command,Arguments=_options.Arguments,InheritEnvironmentVariables=false,EnvironmentVariables=environment,ShutdownTimeout=TimeSpan.FromSeconds(2)});
            }
            else
            {
                transport=new HttpClientTransport(new HttpClientTransportOptions{Name=_options.ServerName,Endpoint=new Uri(_options.Endpoint??throw new InvalidOperationException("Endpoint MCP KB ausente.")),TransportMode=HttpTransportMode.StreamableHttp,ConnectionTimeout=TimeSpan.FromSeconds(_options.TimeoutSeconds)});
            }
        return McpClient.CreateAsync(transport);
    }

    public async ValueTask DisposeAsync(){Task<McpClient>? task;lock(_clientLock){task=_client;_client=null;}if(task is not null&&task.IsCompletedSuccessfully)await task.Result.DisposeAsync();}

    private static string Sql(string? value)=>value is null?"NULL":"'"+value.Replace("'","''",StringComparison.Ordinal)+"'";
    private static string SqlArray(IEnumerable<string> values)=>"ARRAY["+string.Join(',',values.Select(Sql))+"]::text[]";
    private static string RequiredString(JsonElement row,string name)=>OptionalString(row,name)??throw new JsonException($"Campo MCP ausente: {name}");
    private static string? OptionalString(JsonElement row,string name)=>row.TryGetProperty(name,out var value)&&value.ValueKind is not JsonValueKind.Null?value.ValueKind==JsonValueKind.String?value.GetString():value.ToString():null;
    private static double Double(JsonElement row,string name)=>row.TryGetProperty(name,out var value)&&((value.ValueKind==JsonValueKind.Number&&value.TryGetDouble(out var number))||(value.ValueKind==JsonValueKind.String&&double.TryParse(value.GetString(),NumberStyles.Float,CultureInfo.InvariantCulture,out number)))?number:0;
    private static bool Boolean(JsonElement row,string name)=>row.TryGetProperty(name,out var value)&&(value.ValueKind==JsonValueKind.True||bool.TryParse(value.ToString(),out var result)&&result);
    private static string[] StringArray(JsonElement row,string name)=>row.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.Array?value.EnumerateArray().Select(x=>x.GetString()??"").ToArray():OptionalString(row,name)?.Trim('{','}').Split(',',StringSplitOptions.RemoveEmptyEntries)??[];
    private static IReadOnlyDictionary<string,string> StringDictionary(JsonElement row,string name)=>row.TryGetProperty(name,out var value)&&value.ValueKind==JsonValueKind.Object?value.EnumerateObject().ToDictionary(x=>x.Name,x=>x.Value.ToString()):new Dictionary<string,string>();
}
