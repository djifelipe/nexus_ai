using System.Globalization;
using System.Text;
using AiGateway.Domain;
using AiGateway.Domain.Tools;
using Microsoft.Extensions.Options;

namespace AiGateway.Application.IntentRouting;

public sealed class RuleBasedIntentRouter(IIntentCatalog catalog, IOptions<AiGatewayOptions> options, IToolCatalog tools) : IIntentRouter
{
    public async Task<IntentResult> RouteAsync(IntentRouterRequest request, CancellationToken cancellationToken)
    {
        var entries = await catalog.GetActiveAsync(request.UserContext.CompanyId, request.UserContext.Permissions, cancellationToken);
        var normalized = Normalize(request.Question);
        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var scored = entries.Select(entry => Score(entry, normalized, tokens, request.UserContext.Screen)).Where(x => x.Score > 0).OrderByDescending(x => x.Score).ToArray();
        if (scored.Length == 0 || scored[0].Score < options.Value.UnknownThreshold)
            return Unknown(tokens);

        var best = scored[0];
        var tied = scored.Where(x => Math.Abs(x.Score - best.Score) < 0.05 && !string.Equals(x.Entry.Module, best.Entry.Module, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (tied.Length > 0 && string.IsNullOrWhiteSpace(request.UserContext.Screen.CurrentModule))
        {
            var choices = tied.Prepend(best).Select(x => x.Entry.Module).Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToArray();
            return new(null, null, null, null, IntentType.Unknown, best.Score, tokens.ToArray(), [], true,
                $"Você pode esclarecer se a pergunta se refere a {string.Join(", ", choices)}?", "rules:ambiguous", choices);
        }

        var candidates = scored.Where(x => x.Score >= options.Value.UnknownThreshold && (best.Score <= options.Value.MultiModuleThreshold || x == best))
            .Select(x => x.Entry.Module).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var requiredTools = RequiredTools(best.Entry, tools);
        return new(best.Entry.Module, best.Entry.Feature, best.Entry.Action, best.Entry.Entity, best.Entry.Type,
            Math.Clamp(best.Score, 0, 1), tokens.ToArray(), requiredTools, false, null, best.ContextMatched ? "rules:screen-context" : "rules:catalog-term", candidates);
    }

    private static (IntentCatalogEntry Entry, double Score, bool ContextMatched) Score(IntentCatalogEntry entry, string normalized, HashSet<string> tokens, ScreenContext screen)
    {
        var matchedTerms = entry.Terms.Count(term => normalized.Contains(Normalize(term), StringComparison.Ordinal));
        var tokenMatches = entry.Terms.SelectMany(term => Normalize(term).Split(' ')).Distinct().Count(tokens.Contains);
        var contextMatched = !string.IsNullOrWhiteSpace(screen.CurrentModule) && string.Equals(screen.CurrentModule, entry.Module, StringComparison.OrdinalIgnoreCase);
        var score = Math.Min(0.95, matchedTerms * 0.45 + tokenMatches * 0.12 + (contextMatched ? 0.25 : 0) + entry.Weight * 0.1);
        return (entry, score, contextMatched);
    }

    internal static string Normalize(string value)
    {
        var decomposed = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static IntentResult Unknown(HashSet<string> tokens) => new(null, null, null, null, IntentType.Unknown, 0, tokens.ToArray(), [], false, null, "rules:no-match", []);

    private static IReadOnlyList<string> RequiredTools(IntentCatalogEntry entry, IToolCatalog tools)
    {
        string? name = entry.Type switch
        {
            IntentType.PermissionCheck => ReadOnlyToolNames.PermissionCheck,
            IntentType.DataQuery when Contains(entry, "estoque", "produto", "saldo", "inventario") => ReadOnlyToolNames.InventoryGetBalance,
            IntentType.DataQuery when Contains(entry, "documento", "invoice", "nota", "nfe", "nf-e") => ReadOnlyToolNames.InvoiceGetStatus,
            IntentType.DataQuery when Contains(entry, "cliente", "customer", "crm", "cadastro") => ReadOnlyToolNames.CustomerGetSummary,
            IntentType.HowTo when !string.IsNullOrWhiteSpace(entry.Action) => ReadOnlyToolNames.WorkflowGet,
            _ => null
        };
        return name is not null && tools.TryGet(name, out _) ? [name] : [];
    }

    private static bool Contains(IntentCatalogEntry entry, params string[] terms)
    {
        var text = Normalize(string.Join(' ', new[] { entry.Module, entry.Feature, entry.Action, entry.Entity }.Where(x => x is not null)!));
        return terms.Any(term => text.Contains(Normalize(term), StringComparison.Ordinal));
    }
}
