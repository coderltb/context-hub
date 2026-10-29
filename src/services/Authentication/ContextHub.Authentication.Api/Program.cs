// Use the native host provider chain for appsettings, environment-specific settings, Development secrets and environment overrides.
var builder = WebApplication.CreateBuilder(args);

// Host-owned logging reuses the existing masker and correlation behavior; credentials are never startup log data.
builder.Services.AddLoggingInfrastructure(
    builder.Configuration,
    builder.Environment.ApplicationName,
    builder.Environment.EnvironmentName);
builder.Services.AddAuthenticationApi(builder.Configuration, builder.Environment);

// Build after registrations are complete, then compose the service pipeline once before accepting requests.
var app = builder.Build();
app.UseAuthenticationApi();
await app.RunAsync();
