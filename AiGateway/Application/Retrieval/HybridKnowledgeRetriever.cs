using AiGateway.Domain;
using Microsoft.Extensions.Options;

namespace AiGateway.Application.Retrieval;

public sealed class HybridKnowledgeRetriever(IKnowledgeRepository repository, IEmbeddingClient embeddings, ITokenEstimator tokens, IOptions<AiGatewayOptions> options) : IKnowledgeRetriever
{
    public async Task<RetrievalResult> RetrieveAsync(RetrievalRequest request, CancellationToken cancellationToken)
    {
        var maxResults = Math.Min(request.MaxResults, options.Value.MaxResults);
        var maxTokens = Math.Min(request.MaxContextTokens, options.Value.MaxContextTokens);
        var structuredTask = repository.SearchStructuredAsync(request, cancellationToken);
        var embedding = await embeddings.CreateAsync(request.Question, cancellationToken);
        var vectorTask = repository.SearchVectorAsync(request, embedding, cancellationToken);
        await Task.WhenAll(structuredTask, vectorTask);

        var candidates = (await structuredTask).Concat(await vectorTask)
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.FinalScore).First())
            .OrderByDescending(item => item.IsCritical).ThenByDescending(item => item.FinalScore).ToArray();
        var selected = new List<KnowledgeItem>();
        var usedTokens = 0;
        foreach (var item in candidates)
        {
            var cost = tokens.Estimate(item.Title) + tokens.Estimate(item.Content);
            if (selected.Count >= maxResults || usedTokens + cost > maxTokens) continue;
            selected.Add(item);
            usedTokens += cost;
        }
        return new(selected, new(["sql", "pgvector"], ["tenant", "erp-version", "permissions", "active", "published", "validity", "language"], candidates.Length,
            candidates.Length > maxResults, candidates.Sum(x => tokens.Estimate(x.Content)) > maxTokens));
    }
}
