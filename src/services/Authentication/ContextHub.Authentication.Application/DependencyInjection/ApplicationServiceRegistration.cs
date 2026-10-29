namespace ContextHub.Authentication.Application.DependencyInjection;

/// <summary>
/// Registers Authentication use cases without configuring transport, Identity stores or startup mapping.
/// </summary>
public static class ApplicationServiceRegistration
{
    /// <summary>
    /// Registers generated Mediator dispatch and the explicit policies and validators for administrative use cases.
    /// </summary>
    /// <param name="services">The host container owning scoped account operations.</param>
    /// <returns>The same collection for Infrastructure registration.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.Assemblies = [typeof(ApplicationServiceRegistration)];
        });

        // Native discovery stays within this feature assembly; no duplicate hand-maintained validator list.
        services.AddValidatorsFromAssembly(typeof(ApplicationServiceRegistration).Assembly, ServiceLifetime.Transient);

        // The generic constraint limits this policy to Admin requests; other protected requests still fail closed.
        services.AddScoped(typeof(IRequestAuthorizationPolicy<>), typeof(AdminRequestAuthorizationPolicy<>));

        return services;
    }
}
