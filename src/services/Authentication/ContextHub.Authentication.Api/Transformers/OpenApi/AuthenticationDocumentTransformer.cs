namespace ContextHub.Authentication.Api.Transformers.OpenApi;

/// <summary>
/// Documents Bearer or Development OAuth2 security using actual endpoint authorization metadata.
/// </summary>
/// <param name="apiResponseBoundary">The shared runtime response boundary.</param>
/// <param name="authenticationOptions">The canonical configured issuer.</param>
/// <param name="environment">Restricts interactive documentation to Development.</param>
public sealed class AuthenticationDocumentTransformer(
    ApiResponseBoundary apiResponseBoundary,
    IOptions<AuthenticationServiceOptions> authenticationOptions,
    IHostEnvironment environment)
    : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    /// <summary>
    /// Defines document identity and reusable native security schemes without authenticating endpoints.
    /// </summary>
    /// <param name="document">The native document receiving its scheme definition.</param>
    /// <param name="context">The document generation context.</param>
    /// <param name="cancellationToken">The native document generation cancellation token.</param>
    /// <returns>The completed document transformation.</returns>
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info.Title = ApiValueConstants.DOCUMENT_TITLE;
        document.Info.Version = apiResponseBoundary.ApiVersion;
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[ApiValueConstants.BEARER_SCHEME] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = ApiValueConstants.BEARER_HTTP_SCHEME
        };

        // Swagger needs only an access token; the Admin UI retains its separate OIDC identity-token flow.
        if (environment.IsDevelopment())
        {
            var issuer = authenticationOptions.Value.Issuer.TrimEnd('/');
            document.Components.SecuritySchemes[ApiValueConstants.OAUTH_SCHEME] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri(issuer + AuthenticationConstants.Endpoints.AUTHORIZE),
                        TokenUrl = new Uri(issuer + AuthenticationConstants.Endpoints.TOKEN),
                        Scopes = new Dictionary<string, string>
                        {
                            [AuthenticationConstants.ADMIN_SCOPE] = ApiValueConstants.ADMIN_SCOPE_DESCRIPTION
                        }
                    }
                }
            };
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Uses the operation's actual authorization metadata rather than matching paths back to another description list.
    /// </summary>
    /// <param name="operation">The operation whose documented requirement follows its runtime metadata.</param>
    /// <param name="context">The native operation context, including its own API description and document.</param>
    /// <param name="cancellationToken">The native operation generation cancellation token.</param>
    /// <returns>The completed operation transformation.</returns>
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        // Anonymous registration takes precedence over inherited Admin authorization, just as it does at runtime.
        if (metadata.OfType<IAllowAnonymous>().Any() || !metadata.OfType<IAuthorizeData>().Any() ||
            !apiResponseBoundary.Contains(new PathString("/" + context.Description.RelativePath)))
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(ApiValueConstants.BEARER_SCHEME, context.Document)] = []
        });

        // Separate requirements are alternatives; a caller supplies Bearer OR the OAuth access token.
        if (environment.IsDevelopment())
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(ApiValueConstants.OAUTH_SCHEME, context.Document)] =
                    [AuthenticationConstants.ADMIN_SCOPE]
            });
        }

        return Task.CompletedTask;
    }
}
