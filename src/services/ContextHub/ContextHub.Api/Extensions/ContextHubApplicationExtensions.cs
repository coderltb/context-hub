namespace ContextHub.Api.Extensions;

/// <summary>
/// Preserves native transport ordering while limiting Admin envelopes to the selected REST prefix.
/// </summary>
public static class ContextHubApplicationExtensions
{
    /// <summary>
    /// Maps the approved Admin, MCP and anonymous health surfaces.
    /// </summary>
    /// <param name="app">The built Context Hub host.</param>
    /// <returns>The same application.</returns>
    public static WebApplication UseContextHubApi(this WebApplication app)
    {
        // Observe the final adapted status under the same correlation identifier.
        app.UseCorrelation();
        app.UseRequestLogging();
        app.UseApiErrorHandling();

        // Match the endpoint before evaluating origins or spending the pre-authentication ingress allowance.
        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseCors(HubConstants.CORS_POLICY);
        app.UseMiddleware<IngressRateLimitMiddleware>();

        // Native introspection authenticates only the policy-selected scheme; health remains independent of it.
        app.UseAuthentication();
        app.UseAuthorization();

        // Apply the authenticated client budget before inspecting protocol content or dispatching tools.
        app.UseMiddleware<ClientRateLimitMiddleware>();
        app.UseMiddleware<McpProtocolMiddleware>();

        // Development documentation keeps native OAuth/PKCE behavior without exposing a production UI.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
            app.UseApiSwaggerUi(options =>
            {
                options.RoutePrefix = ApiDocumentationConstants.SWAGGER_PREFIX;
                options.SwaggerEndpoint(ApiDocumentationConstants.DOCUMENT_PATH, ApiDocumentationConstants.DOCUMENT_TITLE);
                options.OAuthClientId(ApiDocumentationConstants.SWAGGER_CLIENT_ID);
                options.OAuthScopes(McpApplicationConstants.ADMIN_SCOPE);
                options.OAuthUsePkce();
            });
        }

        // Endpoint policies select the correct actor; health probes intentionally remain anonymous.
        app.MapControllers();
        app.MapMcp(HubConstants.MCP_PATH).RequireAuthorization(HubConstants.MCP_POLICY);
        app.MapHealthChecks(
            ApiRouteConstants.LIVENESS,
            new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains(HealthConstants.LIVENESS_TAG),
                ResponseWriter = HealthResponseWriter.WriteLivenessAsync
            }).AllowAnonymous();

        app.MapHealthChecks(
            ApiRouteConstants.READINESS,
            new HealthCheckOptions
            {
                Predicate = registration => registration.Tags.Contains(HealthConstants.READINESS_TAG),
                ResponseWriter = HealthResponseWriter.WriteReadinessAsync
            }).AllowAnonymous();

        return app;
    }
}
