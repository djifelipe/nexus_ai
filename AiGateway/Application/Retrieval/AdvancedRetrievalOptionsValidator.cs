using Microsoft.Extensions.Options;

namespace AiGateway.Application.Retrieval;

public sealed class AdvancedRetrievalOptionsValidator : IValidateOptions<AdvancedRetrievalOptions>
{
    public ValidateOptionsResult Validate(string? name, AdvancedRetrievalOptions options)
    {
        var errors = new List<string>();
        if (options.GraphDepth is < 1 or > 4) errors.Add("GraphDepth deve estar entre 1 e 4.");
        if (options.AllowedGraphRelations.Length == 0 || options.AllowedGraphRelations.Any(string.IsNullOrWhiteSpace)) errors.Add("AllowedGraphRelations deve conter relações válidas.");
        foreach (var (label, weights) in Weights(options))
        {
            if (weights.Sql < 0 || weights.Vector < 0 || weights.Graph < 0 || Math.Abs(weights.Sql + weights.Vector + weights.Graph - 1) > .000001)
                errors.Add($"Os pesos de {label} devem ser não negativos e somar 1.");
        }
        if (options.SearchCacheTtlMinutes <= 0 || options.ResponseCacheTtlMinutes <= 0) errors.Add("TTLs de cache devem ser positivos.");
        if ((options.SearchCacheEnabled || options.ResponseCacheEnabled) && (string.IsNullOrWhiteSpace(options.CacheKeySecret) || options.CacheKeySecret == "development-only-change-me"))
            errors.Add("CacheKeySecret seguro é obrigatório quando cache está habilitado.");
        if (options.SourceTimeoutMs + options.GraphTimeoutMs + options.ProcessingTimeoutMs > options.RetrievalTimeoutMs)
            errors.Add("A soma dos sub-timeouts não pode exceder RetrievalTimeoutMs.");
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static IEnumerable<(string, RetrievalWeightOptions)> Weights(AdvancedRetrievalOptions value)
    {
        yield return (nameof(value.HowTo), value.HowTo);
        yield return (nameof(value.Explanation), value.Explanation);
        yield return (nameof(value.PermissionCheck), value.PermissionCheck);
        yield return (nameof(value.ImpactAnalysis), value.ImpactAnalysis);
        yield return (nameof(value.Default), value.Default);
    }
}
