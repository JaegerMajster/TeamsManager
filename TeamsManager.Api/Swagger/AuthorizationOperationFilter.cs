using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace TeamsManager.Api.Swagger
{
    /// <summary>
    /// Filtr dodający informacje o wymaganiach autoryzacji do operacji w dokumentacji Swagger
    /// </summary>
    public class AuthorizationOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var hasAuthorize = context.MethodInfo.DeclaringType?.GetCustomAttributes(true)
                .Union(context.MethodInfo.GetCustomAttributes(true))
                .OfType<AuthorizeAttribute>()
                .Any() ?? false;

            var hasAllowAnonymous = context.MethodInfo.GetCustomAttributes(true)
                .OfType<AllowAnonymousAttribute>()
                .Any();

            if (hasAllowAnonymous)
            {
                operation.Summary = operation.Summary?.TrimEnd() + " 🌐";
                operation.Description = (operation.Description ?? "") + "\n\n**🌐 Endpoint publiczny** - nie wymaga uwierzytelniania.";
                return;
            }

            if (hasAuthorize)
            {
                operation.Summary = operation.Summary?.TrimEnd() + " 🔒";
                
                var authInfo = "\n\n**🔒 Wymaga uwierzytelniania**\n\n" +
                              "- **Typ**: JWT Bearer Token z Azure AD\n" +
                              "- **Uprawnienia**: Zgodnie z rolą użytkownika\n" +
                              "- **Kody odpowiedzi**:\n" +
                              "  - `401 Unauthorized` - brak lub nieprawidłowy token\n" +
                              "  - `403 Forbidden` - brak wymaganych uprawnień";

                operation.Description = (operation.Description ?? "") + authInfo;

                if (!operation.Responses.ContainsKey("401"))
                {
                    operation.Responses.Add("401", new OpenApiResponse
                    {
                        Description = "**Unauthorized** - Token JWT jest nieprawidłowy, wygasł lub nie został podany.",
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Schema = new OpenApiSchema
                                {
                                    Type = "object",
                                    Properties = new Dictionary<string, OpenApiSchema>
                                    {
                                        ["message"] = new OpenApiSchema { Type = "string", Example = new Microsoft.OpenApi.Any.OpenApiString("Unauthorized") },
                                        ["detail"] = new OpenApiSchema { Type = "string", Example = new Microsoft.OpenApi.Any.OpenApiString("Token JWT jest wymagany dla tego endpointu") }
                                    }
                                },
                                Example = new Microsoft.OpenApi.Any.OpenApiObject
                                {
                                    ["message"] = new Microsoft.OpenApi.Any.OpenApiString("Unauthorized"),
                                    ["detail"] = new Microsoft.OpenApi.Any.OpenApiString("Bearer token is required")
                                }
                            }
                        }
                    });
                }

                if (!operation.Responses.ContainsKey("403"))
                {
                    operation.Responses.Add("403", new OpenApiResponse
                    {
                        Description = "**Forbidden** - Użytkownik jest uwierzytelniony, ale nie ma wymaganych uprawnień.",
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["application/json"] = new OpenApiMediaType
                            {
                                Schema = new OpenApiSchema
                                {
                                    Type = "object",
                                    Properties = new Dictionary<string, OpenApiSchema>
                                    {
                                        ["message"] = new OpenApiSchema { Type = "string", Example = new Microsoft.OpenApi.Any.OpenApiString("Forbidden") },
                                        ["detail"] = new OpenApiSchema { Type = "string", Example = new Microsoft.OpenApi.Any.OpenApiString("Insufficient permissions for this operation") }
                                    }
                                },
                                Example = new Microsoft.OpenApi.Any.OpenApiObject
                                {
                                    ["message"] = new Microsoft.OpenApi.Any.OpenApiString("Forbidden"),
                                    ["detail"] = new Microsoft.OpenApi.Any.OpenApiString("Insufficient permissions for this operation")
                                }
                            }
                        }
                    });
                }

                if (operation.Security?.Any() != true)
                {
                    operation.Security = new List<OpenApiSecurityRequirement>
                    {
                        new OpenApiSecurityRequirement
                        {
                            {
                                new OpenApiSecurityScheme
                                {
                                    Reference = new OpenApiReference
                                    {
                                        Type = ReferenceType.SecurityScheme,
                                        Id = "Bearer"
                                    }
                                },
                                Array.Empty<string>()
                            }
                        }
                    };
                }
            }

            if (operation.Tags?.Any(tag => tag.Name.Contains("v1")) == true)
            {
                operation.Description = (operation.Description ?? "") + 
                    "\n\n**📋 Wersjonowanie**: Endpoint dostępny w API v1.0";
            }
        }
    }
} 