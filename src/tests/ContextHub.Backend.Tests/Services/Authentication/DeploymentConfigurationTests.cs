namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Checks deployment configuration through native JSON and environment providers without using real secrets.
/// </summary>
public sealed class DeploymentConfigurationTests
{
    /// <summary>
    /// Inherits common defaults while letting deployment variables replace blank Production fields.
    /// </summary>
    [Fact]
    public void ProductionInheritsDefaultsAndAcceptsEnvironmentOverrides()
    {
        var prefix = $"ContextHub_ProductionTest_{Guid.NewGuid():N}_";
        var variable = prefix + "EmailSettings__ApiKey";
        Environment.SetEnvironmentVariable(variable, "synthetic-production-key");
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AuthenticationTestHostPaths.GetContentRoot())
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Production.json")
                .AddEnvironmentVariables(prefix)
                .Build();

            Assert.Equal("synthetic-production-key", configuration["EmailSettings:ApiKey"]);
            Assert.Equal("Context Hub", configuration["EmailSettings:FromName"]);
            Assert.Equal("00:00:30", configuration["EmailSettings:RequestTimeout"]);
            Assert.Equal("00:00:10", configuration["EmailSettings:Resilience:AttemptTimeout:Timeout"]);
            Assert.Equal("00:01:00", configuration["CacheSettings:DefaultEntryOptions:Expiration"]);
            Assert.Equal("8", configuration["Authentication:RefreshTokenLifetimeHours"]);
            Assert.Equal(string.Empty, configuration.GetConnectionString("Authentication"));
            Assert.Equal(string.Empty, configuration["CacheSettings:RedisSettings:Host"]);
            Assert.Equal(string.Empty, configuration["Authentication:SigningKey"]);
            Assert.Empty(configuration.GetSection("Authentication:RedirectUris").GetChildren());
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }
}
