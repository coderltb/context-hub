namespace ContextHub.Core.DependencyInjection;

/// <summary>
/// Registers service-owned persistence and opt-in auditing without opening or migrating a database.
/// </summary>
public static class PersistenceServiceRegistration
{
    /// <summary>
    /// Registers the generic repository and unit of work for one service-owned database context.
    /// </summary>
    /// <typeparam name="TContext">The concrete context owned by the current API host.</typeparam>
    /// <param name="services">The application service collection.</param>
    /// <returns>The same service collection for registration chaining.</returns>
    public static IServiceCollection AddPersistence<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        Func<IServiceProvider, object> contextFactory = static provider => provider.GetRequiredService<TContext>();

        // One service owns one context. Reject foreign aliases before mutation instead of replacing another registration.
        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(DbContext) &&
                (descriptor.Lifetime != ServiceLifetime.Scoped || descriptor.ImplementationFactory != contextFactory) ||
                descriptor.ServiceType == typeof(IUnitOfWork) &&
                (descriptor.Lifetime != ServiceLifetime.Scoped || descriptor.ImplementationType != typeof(UnitOfWork<TContext>))))
        {
            throw new InvalidOperationException(CoreErrorConstants.DatabaseErrors.PERSISTENCE_CONTEXT_CONFLICT);
        }

        // Only register persistence services; mapping is configured separately once at the composition root.
        services.TryAdd(ServiceDescriptor.Scoped(typeof(DbContext), contextFactory));
        services.TryAddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.TryAddScoped<IUnitOfWork, UnitOfWork<TContext>>();

        return services;
    }

    /// <summary>
    /// Registers a scoped service-owned DbContext and attaches the existing audit interceptor from the same DI scope.
    /// </summary>
    /// <typeparam name="TContext">The owning service's opt-in DbContext type.</typeparam>
    /// <param name="services">The host service collection.</param>
    /// <param name="configure">The service's provider configuration callback, receiving the current context scope.</param>
    /// <returns>The same service collection for separate repository and Unit of Work registration.</returns>
    public static IServiceCollection AddAuditedDbContext<TContext>(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configure)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // Reuse the existing audit engine; the owning service must register its actor accessor explicitly.
        services.AddAudit();

        services.AddDbContext<TContext>(
            (provider, options) =>
        {
            // Configure the service's provider first, then attach the interceptor resolved from this context's scope.
            configure(provider, options);

            options.AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>());
        },
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Scoped);

        // Registration neither connects nor migrates a database and does not alias repository or transaction services.
        return services;
    }

    /// <summary>
    /// Registers opt-in stamping; the host supplies an actor accessor and attaches the interceptor to each selected DbContext.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddAudit(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<AuditSaveChangesInterceptor>();

        return services;
    }

    /// <summary>
    /// Registers the factory and facade; a host must separately register NpgsqlDataSource.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <returns>The same collection for continued configuration.</returns>
    public static IServiceCollection AddDapper(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IDbConnectionFactory, DbConnectionFactory>();
        services.TryAddScoped<IDapperService, DapperService>();

        return services;
    }
}
