namespace ContextHub.Infrastructure.DependencyInjection.Telemetry;

/// <summary>
/// Groups scoped telemetry and review services without adding a new persistence or background engine.
/// </summary>
public static class TelemetryServiceRegistration
{
    /// <summary>
    /// Registers feature ports against repositories using the host's existing context and atomic cache.
    /// </summary>
    /// <param name="services">The Context Hub service container.</param>
    /// <returns>The same collection.</returns>
    public static IServiceCollection AddTelemetryFeatureServices(this IServiceCollection services)
    {
        services.AddScoped<IToolRepository, ToolRepository>();
        services.AddScoped<IRecommendationRepository, RecommendationRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IUsageTelemetryRepository, UsageTelemetryRepository>();
        services.AddScoped<IQualitySignalRepository, QualitySignalRepository>();
        services.AddScoped<IToolStateService, ToolStateService>();
        services.AddScoped<IRecommendationService, RecommendationService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IUsageTelemetryService, UsageTelemetryService>();
        services.AddScoped<IQualitySignalService, QualitySignalService>();
        return services;
    }
}
