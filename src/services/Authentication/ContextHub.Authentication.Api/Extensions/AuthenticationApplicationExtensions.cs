namespace ContextHub.Authentication.Api.Extensions;

/// <summary>
/// Keeps pipeline ordering shared by the executable host and HTTP tests.
/// </summary>
public static class AuthenticationApplicationExtensions
{
    /// <summary>
    /// Activates native authentication and maps the implemented API and SSO surfaces without sample endpoints.
    /// </summary>
    /// <param name="app">The built Authentication host.</param>
    /// <returns>The same host for startup.</returns>
    public static WebApplication UseAuthenticationApi(this WebApplication app)
    {
        app.UseForwardedHeaders();

        // Create correlation first, then record transport completion outside the API error adapter so the final status is observed.
        app.UseCorrelation();
        app.UseRequestLogging();
        app.UseApiErrorHandling();

        // Apply the browser HTTPS policy in deployed environments; local development keeps its native certificate workflow.
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        // Match native Razor trailing-slash variants while keeping protocol and Admin failures outside the HTML branch.
        app.UseWhen(
            context => SsoPageConstants.ROUTES.Values.Contains(context.Request.Path.Value?.TrimEnd('/') ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            branch =>
            branch.UseExceptionHandler(AuthenticationConstants.Endpoints.ERROR_PAGE));
        app.UseHttpsRedirection();

        // Select the endpoint before CORS and identity/policy evaluation; both authorization and filters depend on its metadata.
        app.UseRouting();
        app.UseCors(ApiValueConstants.CORS_POLICY);
        // Native handlers validate the Bearer token; the endpoint policy then checks Owner access.
        app.UseAuthentication();
        app.UseAuthorization();

        // Expose interactive API documentation only in Development, keeping deployment credentials out of public documentation surfaces.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
            app.UseApiSwaggerUi(options =>
            {
                options.RoutePrefix = ApiValueConstants.SWAGGER_ROUTE;
                options.SwaggerEndpoint(ApiValueConstants.OPENAPI_PATH, ApiValueConstants.DOCUMENT_TITLE);
                options.OAuthClientId(AuthenticationConstants.SWAGGER_CLIENT);
                options.OAuthScopes(AuthenticationConstants.ADMIN_SCOPE);
                options.OAuthUsePkce();
            });
        }

        // The self-check never opens the database and retains the shared native health response.
        app.MapHealthChecks(
            ApiRouteConstants.LIVENESS,
            new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(HealthConstants.LIVENESS_TAG),
            ResponseWriter = HealthResponseWriter.WriteLivenessAsync
        }).AllowAnonymous();

        // Database readiness and recovery coordination have independent probe sets and the same safe shared writer.
        app.MapHealthChecks(
            ApiRouteConstants.READINESS,
            new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(AuthenticationInfrastructureConstants.READINESS_HEALTH_TAG),
            ResponseWriter = HealthResponseWriter.WriteReadinessAsync
        }).AllowAnonymous();

        app.MapHealthChecks(
            ApiRouteConstants.RECOVERY_HEALTH,
            new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(AuthenticationInfrastructureConstants.RECOVERY_HEALTH_TAG),
            ResponseWriter = HealthResponseWriter.WriteReadinessAsync
        }).AllowAnonymous();

        // Only checked-in public assets are mapped; credential and enrollment data never become static content.
        app.MapStaticAssets().AllowAnonymous();

        // Map the CQRS HTTP and native Razor entry points after all authentication/error middleware has been composed.
        app.MapControllers();
        app.MapRazorPages();

        return app;
    }
}
