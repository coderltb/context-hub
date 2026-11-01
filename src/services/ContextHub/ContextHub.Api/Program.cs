var builder = WebApplication.CreateBuilder(args);

// Register host-owned logging before feature composition; protocol payloads are never DTO-logged.
builder.Services.AddLoggingInfrastructure(
    builder.Configuration,
    builder.Environment.ApplicationName,
    builder.Environment.EnvironmentName);
builder.Services.AddContextHubApi(builder.Configuration);

var app = builder.Build();
app.UseContextHubApi();
await app.RunAsync();
