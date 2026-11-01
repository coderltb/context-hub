namespace ContextHub.Api.DependencyInjection;

/// <summary>
/// Registers the explicit native MCP surface without scanning business handlers into public tools.
/// </summary>
public static class McpServiceRegistration
{
    /// <summary>
    /// Connects tools, resources and guidance prompts to the scoped Mediator adapters.
    /// </summary>
    /// <param name="services">The Context Hub host container.</param>
    /// <param name="configuration">The native MCP instruction configuration.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddContextHubMcp(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind deployment guidance separately from the fixed executable surface.
        services.AddScoped<McpAdapter>();
        services.Configure<McpServerOptions>(configuration.GetSection(McpProtocolConstants.CONFIGURATION_SECTION));

        // Native stateless transport creates scoped adapters without retaining consumer sessions.
        services.AddMcpServer(options =>
        {
            options.ServerInfo = new Implementation
            {
                Name = McpProtocolConstants.SERVER_NAME,
                Version = McpProtocolConstants.SERVER_VERSION
            };
            options.ProtocolVersion = HubConstants.PROTOCOL_VERSION;
            options.ScopeRequests = true;
        })
        .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
        .WithListToolsHandler((
            context,
            token) => context.Services!.GetRequiredService<McpAdapter>().ListToolsAsync(token))
        .WithCallToolHandler((context, token) => context.Services!.GetRequiredService<McpAdapter>().CallToolAsync(
            context.Params.Name,
            JsonSerializer.SerializeToElement(context.Params.Arguments ?? new Dictionary<string, JsonElement>()),
            token))
        .WithListResourcesHandler((
            context,
            token) => context.Services!.GetRequiredService<McpAdapter>().ListResourcesAsync(token))
        .WithListResourceTemplatesHandler((
            context,
            token) => context.Services!.GetRequiredService<McpAdapter>().ListResourceTemplatesAsync(token))
        .WithReadResourceHandler((
            context,
            token) => context.Services!.GetRequiredService<McpAdapter>().ReadResourceAsync(context.Params.Uri, token))
        .WithListPromptsHandler((
            context,
            token) => context.Services!.GetRequiredService<McpAdapter>().ListPromptsAsync(token))
        .WithGetPromptHandler((
            context,
            token) => context.Services!.GetRequiredService<McpAdapter>().GetPromptAsync(context.Params.Name, context.Params.Arguments, token));

        return services;
    }
}
