using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AiGateway.Domain.Responses;
using AiGateway.Domain;
using Microsoft.Extensions.Options;

namespace AiGateway.Application.Validation;

public sealed partial class DeterministicClaimExtractor(IOptions<AdvancedValidationOptions> options) : IClaimExtractor
{
    public Task<ClaimExtractionResult> ExtractAsync(string answer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var claims = new List<VerifiableClaim>();
        foreach (Match match in SentencePattern().Matches(answer))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var text = match.Value.Trim();
            if (!IsVerifiable(text)) continue;
            if (claims.Count >= options.Value.MaxClaims)
                return Task.FromResult(new ClaimExtractionResult(claims, false, ErrorCodes.ValidationLimitExceeded));
            var citations = CitationPattern().Matches(text).Select(x => x.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            claims.Add(new VerifiableClaim(CreateId(match.Index, text), text, match.Index, match.Length, Kind(text), citations));
        }
        return Task.FromResult(new ClaimExtractionResult(claims, true));
    }

    private static bool IsVerifiable(string value) => value.Length >= 8 && !value.EndsWith('?') && value.Any(char.IsLetter);
    private static ClaimKind Kind(string value) => value.Contains("deve", StringComparison.OrdinalIgnoreCase) || value.Contains("somente", StringComparison.OrdinalIgnoreCase)
        ? ClaimKind.BusinessRule : value.StartsWith("Acesse", StringComparison.OrdinalIgnoreCase) || value.StartsWith("Abra", StringComparison.OrdinalIgnoreCase) || char.IsDigit(value[0])
            ? ClaimKind.Procedural : ClaimKind.Factual;
    private static string CreateId(int start, string value) => $"claim-{start}-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..8].ToLowerInvariant()}";

    [GeneratedRegex(@"(?ms)(?:^|(?<=[.!?])\s+|(?<=\n))[-*\d.\s]*(?<sentence>[^\r\n.!?]+[.!?]?)")]
    private static partial Regex SentencePattern();
    [GeneratedRegex(@"\[([a-zA-Z0-9][a-zA-Z0-9._:-]{0,199})\]")]
    private static partial Regex CitationPattern();
}
