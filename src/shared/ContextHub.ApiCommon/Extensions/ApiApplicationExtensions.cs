namespace ContextHub.ApiCommon.Extensions;

/// <summary>
/// Exposes explicit middleware steps so API hosts can keep their approved pipeline ordering.
/// </summary>
public static class ApiApplicationExtensions
{
    /// <summary>
    /// Loads the public documentation asset once for all requests in this process.
    /// </summary>
    private static readonly Lazy<string> SearchScript = new(() =>
    {
        using var stream = typeof(ApiApplicationExtensions).Assembly
            .GetManifestResourceStream(ApiCommonOpenApiConstants.SEARCH_SCRIPT_RESOURCE)
            ?? throw new InvalidOperationException(ApiCommonOpenApiConstants.SEARCH_SCRIPT_MISSING);
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    });

    /// <summary>
    /// Applies shared Swagger presentation while leaving document routes and OAuth configuration to the host.
    /// </summary>
    /// <param name="app">The host pipeline enabling interactive documentation.</param>
    /// <param name="configure">The host-specific document and authentication settings.</param>
    /// <returns>The same application pipeline.</returns>
    public static IApplicationBuilder UseApiSwaggerUI(
        this IApplicationBuilder app,
        Action<SwaggerUIOptions> configure)
    {
        // The host gates this whole branch by environment, including its embedded public asset.
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == ApiCommonOpenApiConstants.SEARCH_SCRIPT_PATH &&
                HttpMethods.IsGet(context.Request.Method))
            {
                context.Response.ContentType = ApiCommonOpenApiConstants.JAVASCRIPT_CONTENT_TYPE;
                await context.Response.WriteAsync(SearchScript.Value, context.RequestAborted);
                return;
            }

            await next(context);
        });

        return app.UseSwaggerUI(options =>
        {
            options.EnableFilter();
            options.InjectJavascript(ApiCommonOpenApiConstants.SEARCH_SCRIPT_PATH);
            configure(options);
        });
    }

    /// <summary>
    /// Initializes safe request metadata before exception, routing and authentication middleware.
    /// </summary>
    /// <param name="app">The host pipeline.</param>
    /// <returns>The same pipeline builder.</returns>
    public static IApplicationBuilder UseCorrelation(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationMiddleware>();

    /// <summary>
    /// Installs exception and empty-status adapters while leaving correlation and request logging order explicit.
    /// </summary>
    /// <param name="app">The host pipeline with an API boundary already registered.</param>
    /// <returns>The same pipeline builder.</returns>
    public static IApplicationBuilder UseApiErrorHandling(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // The exception adapter sees failures from downstream routing/auth; status pages handle bodyless native failures.
        // Branch the adapters themselves, not just their output, so native protocol failures keep their original pipeline.
        var boundary = app.ApplicationServices.GetRequiredService<ApiResponseBoundary>();
        app.UseWhen(context => boundary.Contains(context.Request.Path), branch =>
        {
            branch.UseExceptionHandler();
            branch.UseApiStatusCodes();
        });

        return app;
    }

    /// <summary>
    /// Formats empty API errors; place before routing, authentication and rate limiting.
    /// </summary>
    /// <param name="app">The host pipeline.</param>
    /// <returns>The same pipeline builder.</returns>
    public static IApplicationBuilder UseApiStatusCodes(this IApplicationBuilder app) =>
        app.UseStatusCodePages(async context =>
        {
            var http = context.HttpContext;
            if (http.RequestServices.GetRequiredService<ApiResponseBoundary>().Contains(http.Request.Path) &&
                http.Response.StatusCode.IsSupportedApiError())
            {
                await http.RequestServices.GetRequiredService<ApiErrorResponseFactory>()
                    .WriteAsync(http, http.Response.StatusCode, http.RequestAborted);
            }
        });
}
