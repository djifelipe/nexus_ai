using AiGateway.Application;
using Microsoft.Extensions.Diagnostics.HealthChecks;
namespace AiGateway.Infrastructure.Mcp;
public sealed class KnowledgeBaseMcpHealthCheck(IKnowledgeBaseMcpClient client):IHealthCheck
{public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,CancellationToken cancellationToken=default)=>await client.CheckHealthAsync(cancellationToken)?HealthCheckResult.Healthy("MCP KB disponível."):HealthCheckResult.Unhealthy("MCP KB indisponível.");}
