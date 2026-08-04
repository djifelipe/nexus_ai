using System.Text.RegularExpressions;
using AiGateway.Domain;

namespace AiGateway.Application.Validation;

public sealed partial class CitationResponseValidator : IResponseValidator
{
    public Task<ResponseValidationResult> ValidateAsync(ResponseValidationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Prompt.Sources.Count == 0)
            return Task.FromResult(Insufficient("Nenhuma fonte autorizada foi recuperada."));
        var allowed = request.Prompt.Sources.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cited = CitationPattern().Matches(request.ModelResponse.Content).Select(x => x.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (cited.Length == 0) return Task.FromResult(Insufficient("A resposta factual não contém citações válidas."));
        var invalid = cited.Where(id => !allowed.Contains(id)).ToArray();
        if (invalid.Length > 0)
            return Task.FromResult(new ResponseValidationResult(ValidationStatus.RequiresReview, "Não foi possível validar as fontes da resposta.", [], [ErrorCodes.InvalidCitation]));
        return Task.FromResult(new ResponseValidationResult(ValidationStatus.Grounded, request.ModelResponse.Content, cited, []));
    }

    private static ResponseValidationResult Insufficient(string reason) => new(ValidationStatus.InsufficientKnowledge,
        "Não encontrei informações suficientes na base de conhecimento para responder com segurança.", [], [reason]);
    [GeneratedRegex(@"\[([a-zA-Z0-9][a-zA-Z0-9._:-]{0,199})\]")]
    private static partial Regex CitationPattern();
}
