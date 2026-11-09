using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CoffeeNChill.ApiGateway.Swagger;

// Microsoft Learn - Use Swagger/OpenAPI with ASP.NET Core
// https://learn.microsoft.com/en-us/aspnet/core/tutorials/web-api-help-pages-using-swagger?view=aspnetcore-10.0
// Swashbuckle.AspNetCore - ASP.NET Core Swagger tooling
// https://github.com/domaindrivendev/Swashbuckle.AspNetCore
public sealed class AuthorizeCheckOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata ?? new List<object>();

        var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any();
        var allowsAnonymous = metadata.OfType<IAllowAnonymous>().Any();

        if (!requiresAuthorization || allowsAnonymous)
        {

            operation.Security = new List<OpenApiSecurityRequirement>();
        }
    }
}
