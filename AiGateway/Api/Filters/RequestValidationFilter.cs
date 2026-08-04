using System.ComponentModel.DataAnnotations;
namespace AiGateway.Api.Filters;
public sealed class RequestValidationFilter<T>:IEndpointFilter where T:class
{public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context,EndpointFilterDelegate next){var model=context.Arguments.OfType<T>().FirstOrDefault();if(model is null)return Results.ValidationProblem(new Dictionary<string,string[]>{{"request",["Corpo da requisição é obrigatório."]}});var results=new List<ValidationResult>();if(Validator.TryValidateObject(model,new ValidationContext(model),results,true))return await next(context);return Results.ValidationProblem(results.GroupBy(x=>x.MemberNames.FirstOrDefault()??"request").ToDictionary(x=>x.Key,x=>x.Select(y=>y.ErrorMessage??"Valor inválido.").ToArray()));}}
