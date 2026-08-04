using System.ComponentModel.DataAnnotations;
using AiGateway.Domain;
namespace AiGateway.Api.Contracts;
public sealed record ChatRequestContract(string? ConversationId,[property:Required,StringLength(8000,MinimumLength=1)]string Message,[property:Required]string CompanyId,[property:Required]string UserId,ChatContextContract? Context,ChatOptionsContract? Options);
public sealed record ChatContextContract(string? CurrentModule,string? CurrentScreen,string? SelectedEntityId);
public sealed record ChatOptionsContract(bool Stream=false,bool IncludeSources=true);
public sealed record ErrorContract(string RequestId,string Code,string Message);
public static class ContractMapping{public static ScreenContext ToDomain(this ChatContextContract? value)=>new(value?.CurrentModule,value?.CurrentScreen,value?.SelectedEntityId);}
