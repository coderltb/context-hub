namespace ContextHub.Observability.DependencyInjection;

/// <summary>
/// Registers console logging and native telemetry independently, without activating exporters or payload capture.
/// </summary>
public static class ObservabilityServiceRegistration
{
    /// <summary>
    /// Registers host-configured telemetry while retaining native framework instrumentation.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="configuration">The host-owned observability configuration.</param>
    /// <param name="serviceName">The internal service identity.</param>
    /// <returns>The builder used for explicit exporter composition.</returns>
    public static OpenTelemetryBuilder AddTelemetry(
        this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Bind a startup snapshot: changing configuration must not mutate filters on a running provider.
        var options = configuration.GetSection(ObservabilityConstants.TELEMETRY_SECTION).Get<TelemetryOptions>() ?? new();

        return RegisterTelemetry(services, serviceName, options);
    }

    /// <summary>
    /// Registers standard HTTP traces and metrics while leaving exporter destination and credentials to the host.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="serviceName">The internal service name used as the resource identity.</param>
    /// <returns>The builder on which the host may explicitly configure approved exporters.</returns>
    public static OpenTelemetryBuilder AddTelemetry(this IServiceCollection services, string serviceName)
    {
        return RegisterTelemetry(services, serviceName, new TelemetryOptions());
    }

    /// <summary>
    /// Replaces framework log providers with host-owned masked console sinks, never a static global logger.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="configuration">Configuration containing Observability:Logging.</param>
    /// <param name="serviceName">The internal service name, not a request-provided value.</param>
    /// <param name="environmentName">The host environment name.</param>
    /// <returns>The same service collection for composition.</returns>
    public static IServiceCollection AddLoggingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        string environmentName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ValidateServiceName(serviceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);

        // Bind startup console format, level and category overrides from Observability:Logging before constructing this host's logger.
        services.AddOptions<LoggingOptions>()
            .Bind(configuration.GetSection(ObservabilityConstants.LOGGING_SECTION))
            .Validate(options => Enum.IsDefined(options.ConsoleFormat),
                ObservabilityErrorConstants.INVALID_CONSOLE_FORMAT)
            .Validate(options => Enum.IsDefined(options.MinimumLevel), ObservabilityErrorConstants.INVALID_LOG_LEVEL)
            .Validate(options => options.MinimumLevelOverrides is not null && options.MinimumLevelOverrides.All(pair =>
                    !string.IsNullOrWhiteSpace(pair.Key) && Enum.IsDefined(pair.Value)),
                ObservabilityErrorConstants.INVALID_LOG_LEVEL_OVERRIDES)
            .ValidateOnStart();

        // Route framework logging through the one Serilog pipeline instead of keeping duplicate console providers.
        services.AddLogging(builder => builder.ClearProviders());
        services.AddSerilog((provider, logger) =>
        {
            // Resolve the validated startup settings from DI; native sinks still apply the shared masking formatter.
            var options = provider.GetRequiredService<IOptions<LoggingOptions>>().Value;
            logger.MinimumLevel.Is(options.MinimumLevel)
                .Enrich.FromLogContext()
                .Enrich.With<ActivityEnricher>()
                .Enrich.WithProperty(ObservabilityConstants.SERVICE_NAME, serviceName)
                .Enrich.WithProperty(ObservabilityConstants.ENVIRONMENT, environmentName);

            // Startup validation guarantees overrides; Serilog applies native category-prefix matching.
            foreach (var (category, level) in options.MinimumLevelOverrides!)
            {
                logger.MinimumLevel.Override(category, level);
            }

            // A null stderr threshold keeps every primary event, including Error/Fatal, on stdout.
            logger.WriteTo.Console(new MaskedConsoleFormatter(options.ConsoleFormat == ConsoleLogFormat.Text),
                standardErrorFromLevel: null);
            if (options.ConsoleFormat == ConsoleLogFormat.Both)
            {
                // This second sink is an optional human copy; shippers must ingest application stdout only.
                logger.WriteTo.Console(new MaskedConsoleFormatter(useText: true), standardErrorFromLevel: LogEventLevel.Verbose);
            }
        }, preserveStaticLogger: true);

        // No exporter or body capture is registered; deployment owns shipping and no payload is collected.
        return services;
    }

    /// <summary>
    /// Composes only the approved signals and validates trace filters before any provider is constructed.
    /// </summary>
    /// <param name="services">The host service collection.</param>
    /// <param name="serviceName">The bounded resource identity.</param>
    /// <param name="options">The startup-only instrumentation settings.</param>
    /// <returns>The native builder; exporters remain an explicit host decision.</returns>
    private static OpenTelemetryBuilder RegisterTelemetry(IServiceCollection services, string serviceName, TelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ValidateServiceName(serviceName);

        var paths = options.ExcludedServerPaths;
        if (paths is null || paths.Length > ObservabilityConstants.MAX_EXCLUDED_PATHS ||
            paths.Any(path => string.IsNullOrWhiteSpace(path) || !path.StartsWith('/') || path.TrimEnd('/').Length == 0 ||
                              path.Length > ObservabilityConstants.MAX_EXCLUDED_PATH_LENGTH ||
                              path.Contains('?') || path.Contains('#') || path.Any(char.IsControl)))
        {
            throw new ArgumentException(ObservabilityErrorConstants.INVALID_EXCLUDED_PATHS, nameof(options));
        }

        // Segment matching avoids excluding /healthy merely because /health was configured.
        var excludedPaths = paths.Select(path => new PathString(path.TrimEnd('/'))).ToArray();
        var builder = services.AddOpenTelemetry().ConfigureResource(resource => resource.AddService(serviceName));
        if (options.TracingEnabled)
        {
            builder.WithTracing(tracing =>
            {
                tracing.AddSource(ObservabilityConstants.ACTIVITY_SOURCE);

                // Inbound and outbound HTTP instrumentation are standard, not separate feature switches.
                tracing.AddAspNetCoreInstrumentation(instrumentation =>
                {
                    instrumentation.RecordException = false;
                    instrumentation.Filter = context => !excludedPaths.Any(path => context.Request.Path.StartsWithSegments(path));
                });
                tracing.AddHttpClientInstrumentation(instrumentation => instrumentation.RecordException = false);

                // Keep redaction ahead of exporters so enabling a signal never enables payload collection.
                tracing.AddProcessor(new SafeActivityProcessor());
            });
        }

        if (options.MetricsEnabled)
        {
            builder.WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();
            });
        }

        return builder;
    }

    /// <summary>
    /// Rejects missing or unbounded host identity before allocating logging or telemetry services.
    /// </summary>
    /// <param name="serviceName">The host-owned identity.</param>
    private static void ValidateServiceName(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName) || serviceName.Length > ObservabilityConstants.MAX_SERVICE_NAME_LENGTH)
        {
            throw new ArgumentException(ObservabilityErrorConstants.INVALID_SERVICE_NAME, nameof(serviceName));
        }
    }
}
