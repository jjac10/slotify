using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Slotify.API.OpenApi;

/// <summary>
/// Declara el esquema de seguridad JWT Bearer en el documento OpenAPI para que la
/// UI (Scalar) muestre el botón "Authorize" y aplique el token a las peticiones.
/// </summary>
public sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authSchemeProvider)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var schemes = await authSchemeProvider.GetAllSchemesAsync();
        if (schemes.All(s => s.Name != "Bearer"))
            return;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Pega el accessToken del login/registro (sin el prefijo 'Bearer ').",
        };

        var requirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        };

        foreach (var pathItem in document.Paths.Values)
        {
            if (pathItem.Operations is null)
                continue;
            foreach (var operation in pathItem.Operations.Values)
            {
                operation.Security ??= [];
                operation.Security.Add(requirement);
            }
        }
    }
}
