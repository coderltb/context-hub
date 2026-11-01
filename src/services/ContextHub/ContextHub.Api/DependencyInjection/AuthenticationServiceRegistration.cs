namespace ContextHub.Api.DependencyInjection;

/// <summary>
/// Configures native resource validation and policies without accepting credentials at anonymous endpoints.
/// </summary>
public static class AuthenticationServiceRegistration
{
    /// <summary>
    /// Registers uncached introspection against the Authentication service and exact actor/scope policies.
    /// </summary>
    /// <param name="services">The Context Hub host container.</param>
    /// <param name="configuration">The host configuration supplying authority and backchannel credentials.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddContextHubAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ResourceServerOptions>(configuration.GetSection(HubConstants.AUTHENTICATION_SECTION));
        services.AddCurrentUser();

        // No default scheme: health and documentation do not trigger introspection on irrelevant Bearer headers.
        services.AddAuthentication();
        ConfigureTokenValidation(services);
        ConfigureAuthorization(services);

        return services;
    }

    /// <summary>
    /// Connects the native validation handler to uncached introspection and safe dependency-failure responses.
    /// </summary>
    /// <param name="services">The host container supplying the resource-server options.</param>
    private static void ConfigureTokenValidation(IServiceCollection services)
    {
        services.AddOpenIddict().AddValidation(options =>
        {
            options.AddAudiences(HubConstants.AUDIENCE);
            options.UseIntrospection();
            options.UseSystemNetHttp().ConfigureHttpClient(client =>
                client.Timeout = TimeSpan.FromSeconds(HubConstants.INTROSPECTION_TIMEOUT_SECONDS));
            options.UseAspNetCore()
                .DisableAccessTokenExtractionFromBodyForm()
                .DisableAccessTokenExtractionFromQueryString();

            // Native introspection server errors denote a failed backchannel; invalid credentials retain native 401/403.
            options.AddEventHandler<OpenIddictValidationEvents.ProcessChallengeContext>(handler => handler
                .SetOrder(
                    OpenIddictValidationAspNetCoreHandlers.AttachHttpResponseCode<OpenIddictValidationEvents.ProcessChallengeContext>.Descriptor.Order +
                    HubConstants.DEPENDENCY_FAILURE_HANDLER_ORDER_OFFSET)
                .UseInlineHandler(context =>
                {
                    if (context.Response.Error == OpenIddictConstants.Errors.ServerError)
                    {
                        context.Transaction.GetHttpRequest()!.HttpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    }

                    return ValueTask.CompletedTask;
                }));
        });

        // Resolve issuer and credentials through native options without creating a second service provider.
        services.AddOptions<OpenIddictValidationOptions>()
            .Configure<IOptions<ResourceServerOptions>>((native, resourceServerOptions) =>
            {
                var settings = resourceServerOptions.Value;
                native.Issuer = Uri.TryCreate(settings.Authority, UriKind.Absolute, out var authority) ? authority : null;
                native.ClientId = settings.ClientId;
                native.ClientSecret = settings.ClientSecret;
                native.TokenValidationParameters.RoleClaimType = OpenIddictConstants.Claims.Role;
                native.TokenValidationParameters.NameClaimType = OpenIddictConstants.Claims.Name;
            });
    }

    /// <summary>
    /// Keeps Owner REST and client MCP grants distinct after native token validation.
    /// </summary>
    /// <param name="services">The host container registering endpoint policies.</param>
    private static void ConfigureAuthorization(IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            // A client credential cannot acquire human administration rights even when it carries a scope claim.
            options.AddPolicy(
                HubConstants.ADMIN_POLICY,
                policy => policy
                    .AddAuthenticationSchemes(HubConstants.VALIDATION_SCHEME)
                    .RequireAuthenticatedUser()
                    .RequireClaim(McpApplicationConstants.ACTOR_TYPE, McpApplicationConstants.USER_ACTOR)
                    .RequireRole(McpApplicationConstants.OWNER_ROLE)
                    .RequireAssertion(context => context.User.HasScope(McpApplicationConstants.ADMIN_SCOPE)));

            // Each MCP request needs an application identity; tool-specific grants remain checked at dispatch.
            options.AddPolicy(
                HubConstants.MCP_POLICY,
                policy => policy
                    .AddAuthenticationSchemes(HubConstants.VALIDATION_SCHEME)
                    .RequireAuthenticatedUser()
                    .RequireClaim(McpApplicationConstants.ACTOR_TYPE, McpApplicationConstants.CLIENT_ACTOR)
                    .RequireClaim(McpApplicationConstants.CLIENT_ID)
                    .RequireAssertion(context => context.User.HasScope(McpApplicationConstants.MCP_SCOPE)));
        });
    }
}
