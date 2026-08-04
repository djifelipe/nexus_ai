using AiGateway.Application;
using AiGateway.Domain;

namespace AiGateway.Infrastructure.Graph;

public sealed class McpGraphKnowledgeExpander(IKnowledgeRepository repository) : IGraphKnowledgeExpander
{
    public async Task<GraphExpansionResult> ExpandAsync(GraphExpansionRequest request, CancellationToken cancellationToken)
    {
        if (request.MaxDepth is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(request.MaxDepth));
        if (request.AllowedRelations.Count == 0) throw new AiGatewayException(ErrorCodes.RetrievalFilterIntegrity, "A política de relações do grafo está vazia.");
        var result = await repository.ExpandGraphAsync(request, cancellationToken);
        if (!result.FiltersVerified) throw new AiGatewayException(ErrorCodes.RetrievalFilterIntegrity, "O grafo não confirmou os filtros obrigatórios.");
        var paths = result.Paths.Where(path => path.Depth <= request.MaxDepth && path.Edges.All(edge => request.AllowedRelations.Contains(edge.Relation))).Take(request.MaxPaths).ToArray();
        return result with { Paths = paths, Items = result.Items.Take(request.MaxNodes).ToArray(), MaximumDepth = paths.Select(x => x.Depth).DefaultIfEmpty().Max() };
    }
}
