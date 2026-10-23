namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.DependencyInjection;

/// <summary>
/// Verifies native binding and startup validation without a custom options-validation engine.
/// </summary>
public sealed class OptionsRegistrationTests
{
    /// <summary>
    /// Binds strongly typed values and retains the framework's provider precedence.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task Options_BindLatestProviderAndValidateAtStartup()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Transport:Limit"] = "2" })
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Transport:Limit"] = "7" }).Build();
        using var host = new HostBuilder().ConfigureServices(services => services
            .AddConfiguredOptions<TransportOptions>(configuration, "Transport")
            .Validate(options => options.Limit > 0, "Transport limit must be positive.")).Build();

        await host.StartAsync();

        Assert.Equal(7, host.Services.GetRequiredService<IOptions<TransportOptions>>().Value.Limit);
        await host.StopAsync();
    }

    /// <summary>
    /// Fails before requests when caller-supplied validation rejects configured values.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task Options_InvalidValueFailsStartupWithoutLoggingSecret()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Transport:Limit"] = "0",
            ["Transport:Credential"] = "synthetic-secret"
        }).Build();
        using var host = new HostBuilder().ConfigureServices(services => services
            .AddConfiguredOptions<TransportOptions>(configuration, "Transport")
            .Validate(options => options.Limit > 0, "Transport limit must be positive.")).Build();

        var failure = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.DoesNotContain("synthetic-secret", failure.ToString(), StringComparison.Ordinal);
        Assert.Contains("Transport limit must be positive.", failure.Failures);
    }

    /// <summary>
    /// Rejects absent required sections instead of silently binding default values.
    /// </summary>
    [Fact]
    public void Options_MissingSectionFailsRegistration()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() => services.AddConfiguredOptions<TransportOptions>(configuration, "Transport"));
    }

    /// <summary>
    /// Contains synthetic typed settings for binding and validation boundary checks.
    /// </summary>
    public sealed class TransportOptions
    {
        /// <summary>
        /// Gets or sets a positive caller-validated value.
        /// </summary>
        public int Limit { get; set; }

        /// <summary>
        /// Gets or sets a synthetic credential that must never enter validation messages.
        /// </summary>
        public string Credential { get; set; } = string.Empty;
    }
}
