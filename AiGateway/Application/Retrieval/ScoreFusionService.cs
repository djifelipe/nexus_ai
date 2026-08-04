using AiGateway.Domain;
using AiGateway.Domain.Policies;

namespace AiGateway.Application.Retrieval;

public sealed class ScoreFusionService(IRetrievalRankingPolicy policy)
{
    public IReadOnlyList<KnowledgeItem> Fuse(IntentType intent, IEnumerable<KnowledgeItem> candidates)
    {
        var weights = policy.For(intent);
        return candidates.Select(item =>
        {
            var sql = Clamp(item.SqlScore); var vector = Clamp(item.VectorScore); var graph = Clamp(item.GraphScore);
            var contributions = new[]
            {
                Contribution("sql", item.SqlScore, sql, weights.Sql),
                Contribution("pgvector", item.VectorScore, vector, weights.Vector),
                Contribution("graph", item.GraphScore, graph, weights.Graph)
            };
            return item with
            {
                FinalScore = contributions.Sum(x => x.WeightedScore),
                ScoreContributions = contributions,
                RankingPolicyVersion = policy.Version
            };
        }).OrderByDescending(x => x.IsCritical).ThenByDescending(x => x.FinalScore).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();
    }

    private static ScoreContribution Contribution(string strategy, double raw, double normalized, double weight) => new(strategy, raw, normalized, weight, normalized * weight);
    private static double Clamp(double value) => Math.Clamp(double.IsFinite(value) ? value : 0, 0, 1);
}
