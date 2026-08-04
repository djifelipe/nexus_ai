using System.Text.RegularExpressions;
using AiGateway.Application;

namespace AiGateway.Infrastructure.Security;

public sealed partial class SensitiveDataSanitizer : ISensitiveDataSanitizer
{
    public string Sanitize(string input)
    {
        if(string.IsNullOrEmpty(input)) return input;
        var value=ConnectionStringPattern().Replace(input,"$1=[REDACTED]"); value=SecretPattern().Replace(value,"$1[REDACTED]"); return BearerPattern().Replace(value,"Bearer [REDACTED]");
    }
    [GeneratedRegex(@"(?i)(password|pwd|user id|username|host|database)\s*=\s*[^;\s]+")]
    private static partial Regex ConnectionStringPattern();
    [GeneratedRegex(@"(?i)(api[_-]?key|token|secret|authorization)\s*[:=]\s*[^\s,;]+")]
    private static partial Regex SecretPattern();
    [GeneratedRegex(@"(?i)Bearer\s+[A-Za-z0-9._~+/=-]+")]
    private static partial Regex BearerPattern();
}
