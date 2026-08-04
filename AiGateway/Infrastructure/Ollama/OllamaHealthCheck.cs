using AiGateway.Application;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace AiGateway.Infrastructure.Ollama;

public sealed class OllamaHealthCheck(HttpClient client,IOptions<OllamaOptions> options):IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,CancellationToken cancellationToken=default)
    {
        try
        {
            using var tags=await client.GetAsync("api/tags",cancellationToken);
            if(!tags.IsSuccessStatusCode)return HealthCheckResult.Unhealthy("Ollama indisponível.");
            using var embedding=await client.PostAsJsonAsync("api/embed",new{model=options.Value.EmbeddingModel,input="health"},cancellationToken);
            if(!embedding.IsSuccessStatusCode)return HealthCheckResult.Unhealthy("Modelo de embedding indisponível.");
            using var json=JsonDocument.Parse(await embedding.Content.ReadAsStreamAsync(cancellationToken));
            var dimensions=json.RootElement.GetProperty("embeddings")[0].GetArrayLength();
            return dimensions==options.Value.EmbeddingDimensions?HealthCheckResult.Healthy("Ollama e embeddings compatíveis."):HealthCheckResult.Unhealthy("Dimensão de embedding incompatível.");
        }
        catch{return HealthCheckResult.Unhealthy("Ollama indisponível.");}
    }
}
