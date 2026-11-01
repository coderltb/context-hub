namespace ContextHub.Application.DependencyInjection;

/// <summary>
/// Registers generated use-case dispatch and feature validation without loading persistence or transport services.
/// </summary>
public static class ApplicationServiceRegistration
{
    /// <summary>
    /// Registers this assembly's handlers, validators and fail-closed administration policies.
    /// </summary>
    /// <param name="services">The Context Hub host container.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddContextHubApplication(this IServiceCollection services)
    {
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.Assemblies = [typeof(ApplicationServiceRegistration)];
        });

        // Assembly discovery keeps new use cases within the same shared authorization/validation pipeline.
        services.AddValidatorsFromAssembly(typeof(ApplicationServiceRegistration).Assembly);
        services.AddScoped(typeof(IRequestAuthorizationPolicy<>), typeof(AdminRequestAuthorizationPolicy<>));
        services.AddScoped(typeof(IRequestAuthorizationPolicy<>), typeof(McpRequestAuthorizationPolicy<>));

        return services;
    }
}
