var builder = WebApplication.CreateBuilder(args);

// Bind logging to this host rather than a process-global logger shared by other applications.
builder.Services.AddLoggingInfrastructure(builder.Configuration, builder.Environment.ApplicationName, builder.Environment.EnvironmentName);
builder.Services.AddCorrelation();
builder.Services.AddOpenApi();
builder.Services.AddHealthInfrastructure();

var app = builder.Build();

// Correlation must surround request logging so the completion event sees the accepted request identity.
// Register only this seam: logging must not activate Admin response or exception adapters.
app.UseCorrelation();
app.UseRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Liveness checks only this process; dependency readiness remains a separate service-owned capability.
app.MapHealthChecks(ApiRouteConstants.LIVENESS, new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains(HealthConstants.LIVENESS_TAG),
    ResponseWriter = HealthResponseWriter.WriteLivenessAsync
}).AllowAnonymous();

await app.RunAsync();
