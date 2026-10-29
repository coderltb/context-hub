namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.DependencyInjection;

/// <summary>
/// Exercises shared named and typed registrations through real client factories and request pipelines.
/// </summary>
public sealed class HttpClientRegistrationTests
{
    /// <summary>
    /// Rejects an invalid client name before adding transport services or invoking captured configuration.
    /// </summary>
    /// <param name="name">The invalid null, empty or whitespace-only client name.</param>
    /// <param name="providerAware">Whether configuration receives the service provider.</param>
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(" ", true)]
    public void InvalidName_DoesNotMutateContainerOrRunCallback(string? name, bool providerAware)
    {
        var services = new ServiceCollection().AddLogging();
        var before = services.ToArray();
        var callbackCalled = false;

        var exception = Assert.ThrowsAny<ArgumentException>(() =>
        {
            if (providerAware)
            {
                services.AddHttpClientInfrastructure(name!, (_, _) => callbackCalled = true);
            }
            else
            {
                services.AddHttpClientInfrastructure(name!, _ => callbackCalled = true);
            }
        });

        Assert.Equal("name", exception.ParamName);
        Assert.Equal(before, services.ToArray());
        Assert.False(callbackCalled);
    }

    /// <summary>
    /// Rejects a missing callback eagerly instead of wrapping null in an apparently valid deferred lambda.
    /// </summary>
    /// <param name="providerAware">Whether configuration receives the service provider.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullCallback_IsRejectedBeforeRegistration(bool providerAware)
    {
        var services = new ServiceCollection().AddLogging();
        var before = services.ToArray();

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            if (providerAware)
            {
                services.AddHttpClientInfrastructure("test", (Action<IServiceProvider, HttpClient>)null!);
            }
            else
            {
                services.AddHttpClientInfrastructure("test", (Action<HttpClient>)null!);
            }
        });

        Assert.Equal("configure", exception.ParamName);
        Assert.Equal(before, services.ToArray());
    }

    /// <summary>
    /// Keeps the delegated container guard ahead of deferred client configuration.
    /// </summary>
    /// <param name="providerAware">Whether configuration receives the service provider.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullContainer_DoesNotRunCallback(bool providerAware)
    {
        var callbackCalled = false;

        var exception = Assert.Throws<ArgumentNullException>(() =>
        {
            if (providerAware)
            {
                HttpClientServiceRegistration.AddHttpClientInfrastructure(null!, "test", (_, _) => callbackCalled = true);
            }
            else
            {
                HttpClientServiceRegistration.AddHttpClientInfrastructure(null!, "test", _ => callbackCalled = true);
            }
        });

        Assert.Equal("services", exception.ParamName);
        Assert.False(callbackCalled);
    }

    /// <summary>
    /// Prevents native factory URL/header logging from defeating metadata-only transport logging.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task Registration_DoesNotLogPrivateRequestValues()
    {
        var messages = new ConcurrentQueue<string>();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(new RecordingProvider(messages)));
        services.AddHttpClientInfrastructure("private-upstream", _ => { })
            .ConfigurePrimaryHttpMessageHandler(() => new InspectingHandler(new ConcurrentBag<string>()));
        await using var provider = services.BuildServiceProvider();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.invalid/private-path?token=synthetic-credential");
        request.Headers.Add("X-Request-Identity", "synthetic-identity");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "synthetic-credential");
        request.Content = new StringContent("synthetic-body");

        using var response = await provider.GetRequiredService<IRestClientMultipleService>().SendAsync("private-upstream", request);

        Assert.NotEmpty(messages);
        foreach (var message in messages)
        {
            Assert.DoesNotContain("private-path", message, StringComparison.Ordinal);
            Assert.DoesNotContain("synthetic-credential", message, StringComparison.Ordinal);
            Assert.DoesNotContain("synthetic-identity", message, StringComparison.Ordinal);
            Assert.DoesNotContain("synthetic-body", message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Resolves endpoint configuration from DI and composes an integration-owned typed client and handler.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task TypedClient_UsesServiceOwnedConfigurationAndHandler()
    {
        var seen = new ConcurrentBag<string>();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(new Uri("https://configured.invalid"));
        services.AddTransient<IdentityHandler>();
        services.AddHttpClientInfrastructure<IProbeClient, ProbeClient>("rest",
                (provider, client) => client.BaseAddress = provider.GetRequiredService<Uri>())
            .AddHttpMessageHandler<IdentityHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => new InspectingHandler(seen));
        await using var provider = services.BuildServiceProvider();

        using var response = await provider.GetRequiredService<IProbeClient>().SendAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("configured.invalid:custom", Assert.Single(seen));
    }

    /// <summary>
    /// Lets integrations add a DI-created handler to either named or typed client builders.
    /// </summary>
    /// <param name="typed">Selects the typed-client overload for the same handler composition.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClientBuilder_AllowsAdditionalHandlers(bool typed)
    {
        var seen = new ConcurrentBag<string>();
        var services = new ServiceCollection().AddLogging();
        services.AddTransient<IdentityHandler>();
        var builder = typed
            ? services.AddHttpClientInfrastructure<IProbeClient, ProbeClient>("custom", client => client.BaseAddress = new Uri("https://custom.invalid"))
            : services.AddHttpClientInfrastructure("custom", client => client.BaseAddress = new Uri("https://custom.invalid"));
        builder.AddHttpMessageHandler<IdentityHandler>().ConfigurePrimaryHttpMessageHandler(() => new InspectingHandler(seen));
        await using var provider = services.BuildServiceProvider();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://custom.invalid/work");

        using var response = typed
            ? await provider.GetRequiredService<IProbeClient>().SendAsync()
            : await provider.GetRequiredService<IRestClientMultipleService>().SendAsync("custom", request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("custom.invalid:custom", Assert.Single(seen));
    }

    /// <summary>
    /// Proves configured clients remain separate and mutation failures are never retried.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task NamedClients_KeepConfigurationAndHeadersSeparate()
    {
        var seen = new ConcurrentBag<string>();
        var handler = new InspectingHandler(seen);
        var services = new ServiceCollection().AddLogging();
        services.AddHttpClientInfrastructure("first", client => client.BaseAddress = new Uri("https://first.invalid"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddHttpClientInfrastructure("second", client => client.BaseAddress = new Uri("https://second.invalid"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        var transport = provider.GetRequiredService<IRestClientMultipleService>();
        using var first = new HttpRequestMessage(HttpMethod.Post, "https://first.invalid/work");
        using var second = new HttpRequestMessage(HttpMethod.Post, "https://second.invalid/work");
        first.Headers.Add("X-Request-Identity", "first");
        second.Headers.Add("X-Request-Identity", "second");

        var responses = await Task.WhenAll(transport.SendAsync("first", first), transport.SendAsync("second", second));

        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            response.Dispose();
        }

        Assert.Equal(2, seen.Count);
        Assert.Contains("first.invalid:first", seen);
        Assert.Contains("second.invalid:second", seen);
        using var configured = provider.GetRequiredService<IHttpClientFactory>().CreateClient("first");
        Assert.Equal("first.invalid", configured.BaseAddress!.Host);
        Assert.False(configured.DefaultRequestHeaders.Contains("X-Request-Identity"));
    }

    /// <summary>
    /// Gives typed integrations their configured native client without a second transient registration.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task TypedClient_UsesConfiguredPipeline()
    {
        var seen = new ConcurrentBag<string>();
        var services = new ServiceCollection().AddLogging();
        services.AddHttpClientInfrastructure<IProbeClient, ProbeClient>("typed", client => client.BaseAddress = new Uri("https://typed.invalid"))
            .ConfigurePrimaryHttpMessageHandler(() => new InspectingHandler(seen));
        await using var provider = services.BuildServiceProvider();

        using var response = await provider.GetRequiredService<IProbeClient>().SendAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Single(seen);
        Assert.Contains("typed.invalid:typed", seen);
    }

    /// <summary>
    /// Requires a contract-specific typed client instead of a generic body deserializer.
    /// </summary>
    public interface IProbeClient
    {
        /// <summary>
        /// Sends one sample mutation using the configured client.
        /// </summary>
        /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
        Task<HttpResponseMessage> SendAsync();
    }

    /// <summary>
    /// Represents a service-owned typed integration for registration tests.
    /// </summary>
    /// <param name="client">The native factory-created HTTP client.</param>
    public sealed class ProbeClient(HttpClient client) : IProbeClient
    {
        /// <summary>
        /// Sends a credential-free request whose non-success response must remain intact.
        /// </summary>
        /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
        public async Task<HttpResponseMessage> SendAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/work");
            request.Headers.Add("X-Request-Identity", "typed");

            return await client.SendAsync(request);
        }
    }

    /// <summary>
    /// Records the synthetic upstream identity while exposing retry or credential leakage.
    /// </summary>
    /// <param name="seen">The fixture collection recording observed outgoing request metadata.</param>
    private sealed class InspectingHandler(ConcurrentBag<string> seen) : HttpMessageHandler
    {
        /// <summary>
        /// Produces a transient failure so unsafe retry mistakes become visible in request counts.
        /// </summary>
        /// <param name="request">The native request sent through the configured HTTP handler pipeline.</param>
        /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
        /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Yield();
            seen.Add($"{request.RequestUri!.Host}:{request.Headers.GetValues("X-Request-Identity").Single()}");

            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }
    }

    /// <summary>
    /// Represents an integration-owned handler without placing token acquisition in shared transport.
    /// </summary>
    private sealed class IdentityHandler : DelegatingHandler
    {
        /// <summary>
        /// Changes only this request's synthetic identity before forwarding through the configured pipeline.
        /// </summary>
        /// <param name="request">The native request sent through the configured HTTP handler pipeline.</param>
        /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
        /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Remove("X-Request-Identity");
            request.Headers.Add("X-Request-Identity", "custom");

            return base.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// Captures all native and shared logger categories to detect URI or payload logging at the composed boundary.
    /// </summary>
    /// <param name="messages">The fixture collection receiving formatted diagnostic messages.</param>
    private sealed class RecordingProvider(ConcurrentQueue<string> messages) : ILoggerProvider
    {
        /// <summary>
        /// Shares the thread-safe collector across the actual factory and transport categories.
        /// </summary>
        /// <param name="categoryName">The logger category used to distinguish recorded events.</param>
        /// <returns>The capturing logger for the requested category.</returns>
        public MicrosoftLogger CreateLogger(string categoryName) => new RecordingLogger(messages);

        /// <summary>
        /// Releases no external resource because the collector belongs to the test.
        /// </summary>
        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Captures formatted messages without mocking the HTTP pipeline or rewriting its arguments.
    /// </summary>
    /// <param name="messages">The fixture collection receiving formatted diagnostic messages.</param>
    private sealed class RecordingLogger(ConcurrentQueue<string> messages) : MicrosoftLogger
    {
        /// <summary>
        /// Uses no synthetic scope that could change correlation behavior.
        /// </summary>
        /// <typeparam name="TState">The structured logging state type.</typeparam>
        /// <param name="state">The structured logging state passed through the logger contract.</param>
        /// <returns>A no-op scope used by this test logger.</returns>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <summary>
        /// Enables diagnostic levels so unsafe native logging cannot hide behind filtering.
        /// </summary>
        /// <param name="logLevel">The severity evaluated or recorded by the test logger.</param>
        /// <returns>Whether the tested predicate or contract condition holds.</returns>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>
        /// Records the actual message emitted at the integration boundary.
        /// </summary>
        /// <typeparam name="TState">The structured logging state type.</typeparam>
        /// <param name="logLevel">The severity evaluated or recorded by the test logger.</param>
        /// <param name="eventId">The event identifier supplied with the log entry.</param>
        /// <param name="state">The structured logging state passed through the logger contract.</param>
        /// <param name="exception">The optional exception supplied by the calling logger.</param>
        /// <param name="formatter">The delegate formatting this logger's structured state.</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
    }
}
