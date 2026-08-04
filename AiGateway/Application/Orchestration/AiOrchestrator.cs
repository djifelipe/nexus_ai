using System.Diagnostics;
using AiGateway.Domain;
using Microsoft.Extensions.Options;

namespace AiGateway.Application.Orchestration;

public sealed class AiOrchestrator(IIntentRouter intents,IKnowledgeRetriever retrieval,IPromptBuilder prompts,ILanguageModelClient model,IResponseValidator validator,IAiTelemetry telemetry,IOptions<AiGatewayOptions> options):IAiOrchestrator
{
    public async Task<AiResponse> ExecuteAsync(AiRequest request,CancellationToken cancellationToken)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TotalTimeoutSeconds));using var operation=telemetry.StartRequest(request);var total=Stopwatch.StartNew();
        try
        {
            var(intent,intentMs)=await Timed("intent.route",()=>intents.RouteAsync(new(request.Message,request.UserContext),timeout.Token));
            if(intent.RequiresClarification)return Complete(new(request.RequestId,request.ConversationId,intent.ClarificationQuestion!,ValidationStatus.RequiresReview,intent.Confidence,intent,[],[],Metrics(total,intentMs,0,0,0,0,null,null,0)));
            var(knowledge,retrievalMs)=await Timed("retrieval",()=>retrieval.RetrieveAsync(new(request.Message,intent,request.UserContext,options.Value.MaxResults,options.Value.MaxContextTokens),timeout.Token));
            if(knowledge.Items.Count==0)return Complete(new(request.RequestId,request.ConversationId,"Não encontrei informações suficientes na base de conhecimento para responder com segurança.",ValidationStatus.InsufficientKnowledge,intent.Confidence,intent,[],[],Metrics(total,intentMs,retrievalMs,0,0,0,null,null,0)));
            var(prompt,promptMs)=await Timed("prompt.build",()=>prompts.BuildAsync(new(request.Message,intent,knowledge,request.UserContext),timeout.Token));
            var(generated,modelMs)=await Timed("llm.chat",()=>model.ChatAsync(prompt,timeout.Token));if(generated.HasToolCalls)throw new AiGatewayException(ErrorCodes.UnsupportedTool,"Ferramentas não são suportadas na Fase 1.");
            var(validation,validationMs)=await Timed("response.validate",()=>validator.ValidateAsync(new(generated,prompt),timeout.Token));
            var cited=prompt.Sources.Where(s=>validation.CitedSourceIds.Contains(s.Id,StringComparer.OrdinalIgnoreCase)).Select(s=>new AiSource(s.Id,s.Type,s.Title,s.Version)).ToArray();
            return Complete(new(request.RequestId,request.ConversationId,validation.Answer,validation.Status,Confidence(intent,knowledge,validation),intent,request.IncludeSources?cited:[],validation.Reasons,Metrics(total,intentMs,retrievalMs,promptMs,modelMs,validationMs,generated.PromptTokens,generated.CompletionTokens,prompt.EstimatedTokens)));
        }
        catch(OperationCanceledException ex) when(!cancellationToken.IsCancellationRequested){telemetry.RecordError(ErrorCodes.Timeout);throw new AiGatewayException(ErrorCodes.Timeout,"A solicitação excedeu o tempo limite.",ex);}catch(AiGatewayException ex){telemetry.RecordError(ex.Code);throw;}
    }
    private async Task<(T,long)> Timed<T>(string stage,Func<Task<T>> action){using var scope=telemetry.StartStage(stage);var watch=Stopwatch.StartNew();var result=await action();return(result,watch.ElapsedMilliseconds);}
    private AiResponse Complete(AiResponse response){telemetry.RecordCompleted(response);return response;}
    private static double Confidence(IntentResult intent,RetrievalResult retrieval,ResponseValidationResult validation)=>Math.Clamp(intent.Confidence*.3+(retrieval.Items.Count>0?.35:0)+(validation.Status==ValidationStatus.Grounded?.35:0),0,1);
    private static AiMetrics Metrics(Stopwatch total,long intent,long retrieval,long prompt,long model,long validation,int? pt,int? ct,int context)=>new(total.ElapsedMilliseconds,intent,retrieval,prompt,model,validation,pt,ct,context);
}
