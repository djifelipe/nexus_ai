using AiGateway.Api.Controllers;
using AiGateway.Api.Middleware;
using AiGateway.Infrastructure;
using Microsoft.AspNetCore.Authentication;

var builder=WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddAuthentication("HeaderClaims").AddScheme<AuthenticationSchemeOptions,HeaderAuthenticationHandler>("HeaderClaims",null);
builder.Services.AddAuthorization();
builder.Services.AddAiGateway(builder.Configuration);
var app=builder.Build();
if(app.Environment.IsDevelopment())app.MapOpenApi();
app.UseMiddleware<CorrelationMiddleware>();app.UseMiddleware<SafeExceptionMiddleware>();app.UseHttpsRedirection();app.UseAuthentication();app.UseAuthorization();
app.MapHealthChecks("/health/ready");app.MapAiChat();app.Run();
public partial class Program;
