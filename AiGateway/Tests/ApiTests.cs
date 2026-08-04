using System.Net;
using System.Net.Http.Json;
using AiGateway.Application;
using AiGateway.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace AiGateway.Tests;

public sealed class ApiTests: IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    public ApiTests(ApiFactory factory)=>_client=factory.CreateClient(new WebApplicationFactoryClientOptions{AllowAutoRedirect=false});
    [Fact]public async Task Chat_requires_authentication(){var response=await _client.PostAsJsonAsync("/api/ai/chat",Body());Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);}
    [Fact]public async Task Chat_rejects_payload_identity_conflict(){Authenticate();var response=await _client.PostAsJsonAsync("/api/ai/chat",Body(company:"other"));Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);}
    [Fact]public async Task Chat_returns_grounded_contract(){Authenticate();var response=await _client.PostAsJsonAsync("/api/ai/chat",Body());Assert.Equal(HttpStatusCode.OK,response.StatusCode);var body=await response.Content.ReadFromJsonAsync<AiResponse>();Assert.NotNull(body);Assert.Equal(ValidationStatus.Grounded,body.Status);Assert.Single(body.Sources);Assert.True(response.Headers.Contains("X-Request-Id"));}
    private void Authenticate(){_client.DefaultRequestHeaders.Remove("X-Company-Id");_client.DefaultRequestHeaders.Add("X-Company-Id","company-1");_client.DefaultRequestHeaders.Remove("X-User-Id");_client.DefaultRequestHeaders.Add("X-User-Id","user-1");_client.DefaultRequestHeaders.Remove("X-Erp-Version");_client.DefaultRequestHeaders.Add("X-Erp-Version","5.8.2");_client.DefaultRequestHeaders.Remove("X-Permissions");_client.DefaultRequestHeaders.Add("X-Permissions","Fiscal.NFe.Visualizar");}
    private static object Body(string company="company-1")=>new{conversationId="conversation-1",message="Como cancelar uma NF-e?",companyId=company,userId="user-1",context=new{currentModule="Fiscal",currentScreen="NFeList"},options=new{includeSources=true}};
}

public sealed class ApiFactory:WebApplicationFactory<Program>
{protected override void ConfigureWebHost(IWebHostBuilder builder){builder.ConfigureLogging(logging=>logging.ClearProviders());builder.ConfigureServices(services=>{services.RemoveAll<IAiOrchestrator>();services.AddScoped<IAiOrchestrator,FakeOrchestrator>();});}}
public sealed class FakeOrchestrator:IAiOrchestrator
{public Task<AiResponse> ExecuteAsync(AiRequest request,CancellationToken cancellationToken){var intent=new IntentResult("Fiscal","NFe","NFe.Cancelamento","DocumentoFiscal",IntentType.HowTo,.9,["nfe"],[],false,null,"test",["Fiscal"]);return Task.FromResult(new AiResponse(request.RequestId,request.ConversationId,"Resposta [source-1]",ValidationStatus.Grounded,.9,intent,[new("source-1","workflow","Cancelamento","1")],[],new(1,1,1,1,1,1,1,1,100)));}}
