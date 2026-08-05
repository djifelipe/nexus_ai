using System.Text.RegularExpressions;
using AiGateway.Domain;
using AiGateway.Domain.Responses;
using AiGateway.Domain.Policies;

namespace AiGateway.Application.Validation;

public sealed partial class LexicalSemanticGroundingEvaluator(AdvancedValidationPolicy policy) : ISemanticGroundingEvaluator
{
    public Task<SemanticGroundingResult> EvaluateAsync(IReadOnlyList<VerifiableClaim> claims, IReadOnlyList<KnowledgeItem> authorizedSources, CancellationToken cancellationToken)
    {
        var results = new List<ClaimValidationResult>(claims.Count);
        foreach (var claim in claims)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidates = authorizedSources.Select(source => new { Source = source, Score = Similarity(claim.Text, source.Content) })
                .OrderByDescending(x => x.Score).Take(policy.MaxEvidenceCandidatesPerClaim).ToArray();
            var cited = candidates.Where(x => claim.CitationIds.Contains(x.Source.Id, StringComparer.OrdinalIgnoreCase)).ToArray();
            var best = (cited.Length > 0 ? cited : candidates).FirstOrDefault();
            var contradiction = best is not null && IsContradiction(claim.Text, best.Source.Content);
            var citedEvidence = best is not null && claim.CitationIds.Contains(best.Source.Id, StringComparer.OrdinalIgnoreCase) && best.Score >= Math.Min(.20, policy.SemanticSupportThreshold);
            var status = contradiction ? ClaimGroundingStatus.Contradicted : best is null ? ClaimGroundingStatus.Indeterminate :
                (citedEvidence || best.Score >= policy.SemanticSupportThreshold) ? ClaimGroundingStatus.Supported : ClaimGroundingStatus.Unsupported;
            var evidence = best is null ? [] : new[] { new ClaimEvidence(best.Source.Id, best.Score, contradiction, policy.Version) };
            results.Add(new(claim, status, evidence, status == ClaimGroundingStatus.Supported ? null : ErrorCodes.UnsupportedClaim));
        }
        return Task.FromResult(new SemanticGroundingResult(results, SemanticCheckOutcome.Completed));
    }

    private static double Similarity(string left, string right)
    {
        var a = Tokens(left); var b = Tokens(right); if (a.Count == 0 || b.Count == 0) return 0;
        return (double)a.Intersect(b, StringComparer.OrdinalIgnoreCase).Count() / a.Count;
    }
    private static HashSet<string> Tokens(string value) => WordPattern().Matches(value.ToLowerInvariant()).Select(x => x.Value).Where(x => x.Length > 2).ToHashSet();
    private static bool IsContradiction(string claim, string source) =>
        FixedPeriodPattern().IsMatch(claim) && (source.Contains("depende", StringComparison.OrdinalIgnoreCase) || source.Contains("configurado", StringComparison.OrdinalIgnoreCase));
    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordPattern();
    [GeneratedRegex(@"\b\d+\s+(dia|dias|hora|horas)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FixedPeriodPattern();
}
