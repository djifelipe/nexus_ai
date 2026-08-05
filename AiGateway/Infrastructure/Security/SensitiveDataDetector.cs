using System.Text.RegularExpressions;
using AiGateway.Application;
using AiGateway.Domain;
using AiGateway.Domain.Responses;

namespace AiGateway.Infrastructure.Security;

public sealed partial class SensitiveDataDetector : ISensitiveDataDetector
{
    public IReadOnlyList<SensitiveDataFinding> Detect(string input, UserContext? userContext = null)
    {
        if (string.IsNullOrWhiteSpace(input)) return [];
        var findings = new List<SensitiveDataFinding>();
        Add(ConnectionStringPattern(), SensitiveDataCategory.ConnectionString, "connection_string");
        Add(SecretPattern(), SensitiveDataCategory.Credential, "credential");
        Add(BearerPattern(), SensitiveDataCategory.Token, "token");
        Add(InternalPromptPattern(), SensitiveDataCategory.InternalPrompt, "internal_prompt");
        Add(SqlPattern(), SensitiveDataCategory.Sql, "sql");
        Add(StackTracePattern(), SensitiveDataCategory.StackTrace, "stack_trace");
        Add(BankingPattern(), SensitiveDataCategory.Banking, "banking_data");
        Add(PersonalPattern(), SensitiveDataCategory.Personal, "personal_data");
        Add(FiscalPattern(), SensitiveDataCategory.Fiscal, "fiscal_data");
        Add(PermissionBypassPattern(), SensitiveDataCategory.PermissionBypass, "permission_bypass");
        if (userContext is not null && CrossTenantPattern().Matches(input).Any(x => !x.Groups[1].Value.Equals(userContext.CompanyId, StringComparison.OrdinalIgnoreCase)))
            findings.Add(new(SensitiveDataCategory.CrossTenant, 0, 0, "cross_tenant"));
        return findings;

        void Add(Regex pattern, SensitiveDataCategory category, string code)
        {
            foreach (Match match in pattern.Matches(input)) findings.Add(new(category, match.Index, match.Length, code));
        }
    }

    [GeneratedRegex(@"(?i)(password|pwd|user id|username|host|database)\s*=\s*[^;\s]+")]
    private static partial Regex ConnectionStringPattern();
    [GeneratedRegex(@"(?i)(api[_-]?key|token|secret|authorization)\s*[:=]\s*[^\s,;]+")]
    private static partial Regex SecretPattern();
    [GeneratedRegex(@"(?i)Bearer\s+[A-Za-z0-9._~+/=-]+")]
    private static partial Regex BearerPattern();
    [GeneratedRegex(@"(?i)(system prompt|prompt (interno|do sistema)|ignore (as )?instruções anteriores)")]
    private static partial Regex InternalPromptPattern();
    [GeneratedRegex(@"(?i)\b(select|insert|update|delete|drop|alter)\s+.+\b(from|into|table|set)\b")]
    private static partial Regex SqlPattern();
    [GeneratedRegex(@"(?i)\bat\s+[\w.]+\([^)]*\)\s+in\s+.+:\s*line\s+\d+")]
    private static partial Regex StackTracePattern();
    [GeneratedRegex(@"(?i)\b(ag[eê]ncia|conta|iban|swift)\s*[:=]\s*[\d.-]{4,}")]
    private static partial Regex BankingPattern();
    [GeneratedRegex(@"(?i)\b(cpf|rg|e-?mail|telefone)\s*[:=]\s*[a-z0-9@.+()-]{5,}")]
    private static partial Regex PersonalPattern();
    [GeneratedRegex(@"(?i)\b(cnpj|inscri[cç][aã]o estadual|chave de acesso)\s*[:=]\s*[\d.-]{8,}")]
    private static partial Regex FiscalPattern();
    [GeneratedRegex(@"(?i)(burlar|ignorar|contornar|desabilitar).{0,30}(permiss[aã]o|autoriza[cç][aã]o|acesso)")]
    private static partial Regex PermissionBypassPattern();
    [GeneratedRegex(@"(?i)company(?:Id)?\s*[:=]\s*([a-z0-9_-]+)")]
    private static partial Regex CrossTenantPattern();
}
