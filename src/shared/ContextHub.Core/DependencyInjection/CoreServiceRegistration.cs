namespace ContextHub.Core.DependencyInjection;

/// <summary>
/// Composes reusable messaging, JSON, mapping and HTTP services without configuring business policies or persistence.
/// </summary>
public static class CoreServiceRegistration
{
    /// <summary>
    /// Registers shared transport dependencies and authorization before validation, without adding business handlers or policies.
    /// </summary>
    /// <param name="services">The host container owning shared services.</param>
    /// <returns>The same collection for service-owned configuration.</returns>
    public static IServiceCollection AddCoreInfrastructure(this IServiceCollection services)
    {
        // Configure loaded profiles before any consumer can map; transport registration never scans again.
        services.AddMapping();
        services.AddRestClients();

        // Authorization is outermost so rejected requests cannot run validators or business work.
        services.AddAuthorizationBehavior();
        services.AddValidationBehavior();

        return services;
    }

    /// <summary>
    /// Registers neutral authorization; each service must register policies for its protected request types.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <returns>The same collection; the host explicitly orders its pipeline registrations.</returns>
    public static IServiceCollection AddAuthorizationBehavior(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>)));

        return services;
    }

    /// <summary>
    /// Registers the FluentValidation Mediator pipeline behavior.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <returns>The same service collection for registration chaining.</returns>
    public static IServiceCollection AddValidationBehavior(this IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>)));

        return services;
    }

    /// <summary>
    /// Shares Newtonsoft conventions across REST clients and application consumers using constructor injection.
    /// </summary>
    /// <param name="services">The owning host container.</param>
    /// <returns>The collection for continued composition.</returns>
    public static IServiceCollection AddJsonSerializer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IJsonSerializerService, JsonSerializerService>();

        return services;
    }

    /// <summary>
    /// Applies mapping files from loaded assemblies to the process-wide configuration and compiles them at startup.
    /// </summary>
    /// <param name="services">The composition root configuring mappings once before consumers run.</param>
    /// <returns>The same collection; no configuration or mapper is registered in DI.</returns>
    public static IServiceCollection AddMapping(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Profiles are pure, public and parameterless. Feature assemblies must already be loaded.
        // Complete registration before the first Adapt or projection; never mutate global mappings per request.
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(AppDomain.CurrentDomain.GetAssemblies());
        config.Compile();

        return services;
    }

    /// <summary>
    /// Registers shared JSON and REST services without reconfiguring startup mappings.
    /// </summary>
    /// <param name="services">The owning container.</param>
    /// <returns>The collection for continued composition.</returns>
    public static IServiceCollection AddRestClients(this IServiceCollection services)
    {
        // JSON registration validates the container. Standalone composition calls AddMapping once separately.
        services.AddJsonSerializer();

        services.AddHttpClient();
        services.TryAddTransient<IRestClientMultipleService, RestClientMultipleService>();
        services.TryAddTransient<IThirdPartyApiService, ThirdPartyApiService>();

        return services;
    }

    /// <summary>
    /// Requires a section and schedules native validation before the host starts accepting requests.
    /// </summary>
    /// <typeparam name="TOptions">The service-owned settings type.</typeparam>
    /// <param name="services">The host container.</param>
    /// <param name="configuration">The configuration assembled by the host, including environment overrides.</param>
    /// <param name="sectionName">The required internal configuration section.</param>
    /// <returns>The native builder for explicit caller-owned validation rules.</returns>
    public static OptionsBuilder<TOptions> AddConfiguredOptions<TOptions>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        // Missing required configuration must not silently become a default settings object.
        // The owning service supplies safe validation messages; never embed option values or secrets in them.
        var section = configuration.GetRequiredSection(sectionName);

        return services.AddOptions<TOptions>().Bind(section).ValidateOnStart();
    }
}
