namespace ContextHub.Api.Transformers.OpenApi;

/// <summary>
/// Documents Bearer or Development OAuth2 security using actual endpoint authorization metadata.
/// </summary>
/// <param name="apiResponseBoundary">The shared runtime response boundary.</param>
/// <param name="authenticationOptions">The canonical configured issuer.</param>
/// <param name="environment">Restricts interactive documentation to Development.</param>
public sealed class ContextHubDocumentTransformer(
    ApiResponseBoundary apiResponseBoundary,
    IOptions<ResourceServerOptions> authenticationOptions,
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
        document.Info.Title = ApiDocumentationConstants.DOCUMENT_TITLE;
        document.Info.Version = apiResponseBoundary.ApiVersion;
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[ApiDocumentationConstants.BEARER_SCHEME] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = ApiDocumentationConstants.BEARER_HTTP_SCHEME
        };

        // Swagger needs only an access token; the Admin UI retains its separate OIDC identity-token flow.
        if (environment.IsDevelopment())
        {
            var issuer = authenticationOptions.Value.Authority.TrimEnd('/');
            document.Components.SecuritySchemes[ApiDocumentationConstants.OAUTH_SCHEME] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri(issuer + HubConstants.AUTHORIZE_PATH),
                        TokenUrl = new Uri(issuer + HubConstants.TOKEN_PATH),
                        Scopes = new Dictionary<string, string>
                        {
                            [McpApplicationConstants.ADMIN_SCOPE] = ApiDocumentationConstants.ADMIN_SCOPE_DESCRIPTION
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

        // Document security only inside the REST boundary, honoring anonymous endpoint overrides.
        if (metadata.OfType<IAllowAnonymous>().Any() || !metadata.OfType<IAuthorizeData>().Any() ||
            !apiResponseBoundary.Contains(new PathString("/" + context.Description.RelativePath)))
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(ApiDocumentationConstants.BEARER_SCHEME, context.Document)] = []
        });

        // Separate requirements are alternatives; a caller supplies Bearer OR the OAuth access token.
        if (environment.IsDevelopment())
        {
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(ApiDocumentationConstants.OAUTH_SCHEME, context.Document)] =
                    [McpApplicationConstants.ADMIN_SCOPE]
            });
        }

        return Task.CompletedTask;
    }
}
