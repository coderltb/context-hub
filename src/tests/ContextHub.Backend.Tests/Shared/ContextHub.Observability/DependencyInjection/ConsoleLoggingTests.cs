namespace ContextHub.Backend.Tests.Shared.ContextHub.Observability.DependencyInjection;

/// <summary>
/// Verifies the console format switch without a custom request collector or logger factory.
/// </summary>
[Collection("Process-wide telemetry")]
public sealed class ConsoleLoggingTests
{
    /// <summary>
    /// Masks embedded header credentials and quoted secrets before either native console formatter writes them.
    /// </summary>
    /// <param name="format">The text, JSON or dual-stream logging mode.</param>
    [Theory]
    [InlineData("Text")]
    [InlineData("CompactJson")]
    [InlineData("Both")]
    public void Logging_MasksEmbeddedCredentialTextOnEveryStream(string format)
    {
        var captured = CaptureConsole(CreateConfiguration(format, "Information"), provider =>
            provider.GetRequiredService<ILogger<ConsoleLoggingTests>>().LogInformation(
                "Provider diagnostic {Diagnostic}.",
                "Authorization: Bearer bearer-secret; Authorization: Basic basic-secret; password='quoted secret'; " +
                "provider={\"items\":[{\"access_token\":\"fragment-secret\",\"name\":\"visible\"}]}; path=/reports; " +
                "url=https://fixture-user:uri-secret@example.test/path?api%5Fkey=query-secret&q=visible; " +
                JsonSerializer.Serialize("{\"password\":\"encoded-secret\"}")));

        // Both writes one machine-readable event and one text copy; neither stream may expose credential suffixes.
        foreach (var rendered in new[] { captured.Output, captured.Error }.Where(value => value.Length > 0))
        {
            Assert.DoesNotContain("bearer-secret", rendered);
            Assert.DoesNotContain("basic-secret", rendered);
            Assert.DoesNotContain("quoted secret", rendered);
            Assert.DoesNotContain("fragment-secret", rendered);
            Assert.DoesNotContain("uri-secret", rendered);
            Assert.DoesNotContain("query-secret", rendered);
            Assert.DoesNotContain("encoded-secret", rendered);
            Assert.Contains("visible", rendered);
            Assert.Contains("/reports", rendered);
            Assert.Contains(SharedConstants.LoggingVariables.MASKED_VALUE, rendered);
        }

        Assert.NotEmpty(captured.Output);
        Assert.Equal(format == "Both", captured.Error.Length > 0);
    }

    /// <summary>
    /// Keeps machine-readable events on stdout at every level and sends only the optional text copy to stderr.
    /// </summary>
    /// <param name="format">The console output format selected by the fixture.</param>
    /// <param name="json">The JSON text to deserialize; null or blank input produces the documented empty result.</param>
    /// <param name="textCopy">Whether an additional human-readable stderr copy is expected.</param>
    [Theory]
    [InlineData("Text", false, false)]
    [InlineData("CompactJson", true, false)]
    [InlineData("Both", true, true)]
    public void Logging_OutputModes_KeepErrorAndFatalOnStdout(string format, bool json, bool textCopy)
    {
        var captured = CaptureConsole(CreateConfiguration(format, "Information"), provider =>
        {
            provider.GetRequiredService<ILogger<ConsoleLoggingTests>>().LogError("Operation failed with {Count}.", 2);
            provider.GetRequiredService<Serilog.ILogger>().Fatal("Host stopped with {Count}.", 3);
        });

        var lines = captured.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        if (json)
        {
            using var error = JsonDocument.Parse(lines[0]);
            using var fatal = JsonDocument.Parse(lines[1]);
            Assert.Equal("Error", error.RootElement.GetProperty("@l").GetString());
            Assert.Equal("Fatal", fatal.RootElement.GetProperty("@l").GetString());
        }

        if (textCopy)
        {
            Assert.Equal(2, captured.Error.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.Contains("Operation failed with 2", captured.Error, StringComparison.Ordinal);
            Assert.Contains("Host stopped with 3", captured.Error, StringComparison.Ordinal);
            Assert.False(captured.Error.TrimStart().StartsWith('{'));
        }
        else
        {
            Assert.Empty(captured.Error);
        }
    }

    /// <summary>
    /// Applies category-specific thresholds without weakening the default for unrelated categories.
    /// </summary>
    [Fact]
    public void Logging_CategoryOverrides_FilterOnlyMatchingCategories()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Observability:Logging:MinimumLevel"] = "Information",
            ["Observability:Logging:MinimumLevelOverrides:Microsoft.AspNetCore"] = "Warning",
            ["Observability:Logging:MinimumLevelOverrides:Application.Detail"] = "Debug"
        }).Build();

        var captured = CaptureConsole(configuration, provider =>
        {
            var factory = provider.GetRequiredService<ILoggerFactory>();
            factory.CreateLogger("Microsoft.AspNetCore.Server").LogInformation("Framework information suppressed.");
            factory.CreateLogger("Microsoft.AspNetCore.Server").LogWarning("Framework warning retained.");
            factory.CreateLogger("Application.Detail.Worker").LogDebug("Application detail retained.");
            factory.CreateLogger("Application.Other").LogDebug("Unrelated debug suppressed.");
        });

        Assert.DoesNotContain("Framework information suppressed", captured.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Unrelated debug suppressed", captured.Output, StringComparison.Ordinal);
        Assert.Contains("Framework warning retained", captured.Output, StringComparison.Ordinal);
        Assert.Contains("Application detail retained", captured.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Rejects invalid format, level and override configuration before a host can accept requests.
    /// </summary>
    /// <param name="key">The invalid configuration key.</param>
    /// <param name="value">The unsupported setting.</param>
    [Theory]
    [InlineData("ConsoleFormat", "999")]
    [InlineData("MinimumLevel", "999")]
    [InlineData("MinimumLevelOverrides:Category", "999")]
    [InlineData("MinimumLevelOverrides: ", "Information")]
    public void Logging_InvalidConfiguration_IsRejectedAtStartup(string key, string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"Observability:Logging:{key}"] = value
        }).Build();
        var services = new ServiceCollection();
        services.AddLoggingInfrastructure(configuration, "TestService", "Test");
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    /// <summary>
    /// Retains ordinary URI and path fields while masking credentials and exception details in both console streams.
    /// </summary>
    /// <param name="format">The configured output mode.</param>
    [Theory]
    [InlineData("Text")]
    [InlineData("CompactJson")]
    [InlineData("Both")]
    public void Logging_MasksCredentialsWithoutHidingPaths(string format)
    {
        var captured = CaptureConsole(CreateConfiguration(format, "Information"), provider =>
        {
            var logger = provider.GetRequiredService<ILogger<ConsoleLoggingTests>>();
            logger.LogError(new InvalidOperationException("exception-sentinel"),
                "Request failed at {Uri}, {Path}, {RequestUri}; credential {ApiKey}.",
                "uri-sentinel", "path-sentinel", "request-uri-sentinel", "key-sentinel");
        });

        var rendered = captured.Output + captured.Error;
        Assert.Contains(SharedConstants.LoggingVariables.MASKED_VALUE, rendered, StringComparison.Ordinal);
        Assert.Contains("uri-sentinel", rendered, StringComparison.Ordinal);
        Assert.Contains("path-sentinel", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("key-sentinel", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("exception-sentinel", rendered, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), rendered, StringComparison.Ordinal);
    }

    /// <summary>
    /// Uses the configured diagnostic threshold directly instead of admitting Debug for a collector.
    /// </summary>
    [Fact]
    public void Logging_WarningThreshold_DisablesDebug()
    {
        var configuration = CreateConfiguration("Text", "Warning");
        var services = new ServiceCollection();
        services.AddLoggingInfrastructure(configuration, "TestService", "Test");
        using var provider = services.BuildServiceProvider();
        var logger = provider.GetRequiredService<ILogger<ConsoleLoggingTests>>();

        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
    }

    /// <summary>
    /// Changes only console rendering while retaining structured metadata and sensitive-field masking.
    /// </summary>
    /// <param name="format">The explicitly selected console format.</param>
    [Theory]
    [InlineData("Text")]
    [InlineData("CompactJson")]
    public void Logging_FormatSwitch_WritesMaskedConsoleOutput(string format)
    {
        var originalOutput = Console.Out;
        using var output = new StringWriter();
        Console.SetOut(output);

        try
        {
            var services = new ServiceCollection();
            services.AddLoggingInfrastructure(CreateConfiguration(format, "Information"), "TestService", "Test");
            using (var provider = services.BuildServiceProvider())
            {
                var logger = provider.GetRequiredService<ILogger<ConsoleLoggingTests>>();
                logger.LogInformation("Operation finished with {Count}; credential {ApiKey}.", 2, "private-key");
            }

            var text = output.ToString();
            Assert.Contains("TestService", text, StringComparison.Ordinal);
            Assert.Contains(SharedConstants.LoggingVariables.MASKED_VALUE, text, StringComparison.Ordinal);
            Assert.DoesNotContain("private-key", text, StringComparison.Ordinal);
            if (format == "CompactJson")
            {
                using var document = JsonDocument.Parse(text.Trim());
                Assert.Equal(2, document.RootElement.GetProperty("Count").GetInt32());
                Assert.True(document.RootElement.TryGetProperty("@t", out _));
            }
            else
            {
                Assert.Contains("Operation finished with 2", text, StringComparison.Ordinal);
                Assert.False(text.TrimStart().StartsWith('{'));
            }
        }
        finally
        {
            // Console is process-wide; always release the test override, including assertion failures.
            Console.SetOut(originalOutput);
        }
    }

    /// <summary>
    /// Builds the same two logging settings consumed from appsettings or deployment environment overrides.
    /// </summary>
    /// <param name="format">The console output format selected by the fixture.</param>
    /// <param name="level">The configured minimum diagnostic severity.</param>
    /// <returns>The in-memory configuration consumed by the real registration under test.</returns>
    private static IConfiguration CreateConfiguration(string format, string level)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Observability:Logging:ConsoleFormat"] = format,
            ["Observability:Logging:MinimumLevel"] = level
        }).Build();
    }

    /// <summary>
    /// Exercises the real console sinks while restoring process-wide writers even when logging fails.
    /// </summary>
    /// <param name="configuration">The startup configuration under test.</param>
    /// <param name="write">The ordinary logging calls made through the host container.</param>
    /// <returns>The separate stdout and stderr records.</returns>
    private static (string Output, string Error) CaptureConsole(IConfiguration configuration, Action<IServiceProvider> write)
    {
        var originalOutput = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        Console.SetOut(output);
        Console.SetError(error);

        try
        {
            var services = new ServiceCollection();
            services.AddLoggingInfrastructure(configuration, "TestService", "Test");
            using (var provider = services.BuildServiceProvider())
            {
                write(provider);
            }

            return (output.ToString(), error.ToString());
        }
        finally
        {
            // Console writers belong to the process, not to the disposable logging provider.
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }
    }
}
