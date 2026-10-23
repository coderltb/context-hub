var builder = WebApplication.CreateBuilder(args);

// Bind logging to this host rather than a process-global logger shared by other applications.
builder.Services.AddLoggingInfrastructure(builder.Configuration, builder.Environment.ApplicationName, builder.Environment.EnvironmentName);
builder.Services.AddCorrelation();
builder.Services.AddOpenApi();

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

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
   {
       var forecast = Enumerable.Range(1, 5).Select(index =>
                                    new WeatherForecast
                                    (
                                        DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                                        Random.Shared.Next(-20, 55),
                                        summaries[Random.Shared.Next(summaries.Length)]
                                    ))
                                .ToArray();
       return forecast;
   })
   .WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
