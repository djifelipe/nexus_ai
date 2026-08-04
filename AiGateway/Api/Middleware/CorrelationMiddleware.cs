using System.Diagnostics;
namespace AiGateway.Api.Middleware;
public sealed class CorrelationMiddleware(RequestDelegate next){public async Task InvokeAsync(HttpContext context){var id=context.Request.Headers["X-Request-Id"].FirstOrDefault();if(string.IsNullOrWhiteSpace(id))id=Guid.CreateVersion7().ToString();context.Items["RequestId"]=id;context.Response.Headers["X-Request-Id"]=id;context.Response.Headers["X-Trace-Id"]=Activity.Current?.TraceId.ToString()??context.TraceIdentifier;await next(context);}}
