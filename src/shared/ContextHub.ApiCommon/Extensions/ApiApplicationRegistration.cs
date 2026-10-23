namespace ContextHub.ApiCommon.Extensions;

/// <summary>
/// Exposes explicit middleware steps so API hosts can keep their approved pipeline ordering.
/// </summary>
public static class ApiApplicationRegistration
{
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
