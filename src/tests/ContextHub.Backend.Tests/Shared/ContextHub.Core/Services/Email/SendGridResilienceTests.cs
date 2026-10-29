namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Services.Email;

/// <summary>
/// Exercises the factory-owned SDK transport, including the body buffering performed outside its resilience handler.
/// </summary>
public sealed class SendGridResilienceTests
{
    /// <summary>
    /// Binds native resilience settings from the selected email section and safely translates an actual attempt timeout.
    /// </summary>
    /// <returns>The bounded SDK outcome and one-POST assertions.</returns>
    [Fact]
    public async Task NativeTimeoutOptionsAreBoundAndTimeoutDoesNotRetryPost()
    {
        using var handler = new ProviderHandler(async cancellationToken =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        });
        var services = CreateServices(handler, configure: configuration =>
        {
            configuration["Email:Resilience:AttemptTimeout:Timeout"] = "00:00:01";
            configuration["Email:Resilience:TotalRequestTimeout:Timeout"] = "00:00:02";
        });
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.False(await scope.ServiceProvider.GetRequiredService<IEmailService>()
            .SendAsync(Message(), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>
    /// Verifies repeated composition preserves one pipeline, transient SDK clients and scoped email adapters.
    /// </summary>
    /// <returns>The resolved scope and single-attempt assertions.</returns>
    [Fact]
    public async Task RegistrationIsRepeatableAndPostIsNotRetried()
    {
        using var handler = new ProviderHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        var services = CreateServices(handler);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        Assert.Equal(ServiceLifetime.Transient, Assert.Single(services, value => value.ServiceType == typeof(ISendGridClient)).Lifetime);
        Assert.NotSame(provider.GetRequiredService<ISendGridClient>(), provider.GetRequiredService<ISendGridClient>());
        Assert.Same(emailService, scope.ServiceProvider.GetRequiredService<IEmailService>());
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(CoreConstants.EmailVariables.HTTP_CLIENT_NAME);
        Assert.Equal(TimeSpan.FromSeconds(30), client.Timeout);
        Assert.False(await emailService.SendAsync(Message(), CancellationToken.None));
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>
    /// Opens only the mail circuit and advances the native break duration without sleeping.
    /// </summary>
    /// <returns>The open, half-open and recovered transport assertions.</returns>
    [Fact]
    public async Task CircuitRecoversWithOneHalfOpenProbeAndDoesNotAffectOtherClients()
    {
        var timeProvider = new FakeTimeProvider();
        var probeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProbe = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var providerAttempts = 0;
        using var handler = new ProviderHandler(async cancellationToken =>
        {
            if (Interlocked.Increment(ref providerAttempts) <= 5)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            probeStarted.TrySetResult();
            await releaseProbe.Task.WaitAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        });
        var services = CreateServices(handler);
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddHttpClient("independent-mail-test")
            .ConfigurePrimaryHttpMessageHandler(() => new ProviderHandler(_ =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))))
            .AddHttpResilience();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        // The default minimum throughput is five; each failed POST contributes once and is never replayed.
        for (var index = 0; index < 5; index++)
        {
            Assert.False(await emailService.SendAsync(Message(), CancellationToken.None));
        }

        Assert.False(await emailService.SendAsync(Message(), CancellationToken.None));
        Assert.Equal(5, handler.Calls);
        using var independent = provider.GetRequiredService<IHttpClientFactory>().CreateClient("independent-mail-test");
        using var response = await independent.GetAsync("https://example.invalid/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        timeProvider.Advance(TimeSpan.FromSeconds(16));
        var probe = emailService.SendAsync(Message(), CancellationToken.None);
        await probeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // While one recovery probe is in flight, another call must not reach the provider.
        Assert.False(await emailService.SendAsync(Message(), CancellationToken.None));
        Assert.Equal(6, handler.Calls);
        releaseProbe.TrySetResult();
        Assert.True(await probe);
        Assert.True(await emailService.SendAsync(Message(), CancellationToken.None));
        Assert.Equal(7, handler.Calls);
    }

    /// <summary>
    /// Bounds the SDK's buffering after headers have left Polly, using the outer native HttpClient timeout.
    /// </summary>
    /// <returns>The bounded outcome, single body read and response-disposal assertions.</returns>
    [Fact]
    public async Task StalledBodyIsBoundedAndReadOnlyOnce()
    {
        using var content = new StalledContent();
        using var handler = new ProviderHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = content
        }));
        var services = CreateServices(handler, "00:00:01");
        var logger = new TestLogger<SendGridEmailService>();
        services.AddSingleton<ILogger<SendGridEmailService>>(logger);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Allow native transport initialization and require a body-read signal so a header-phase timeout cannot pass this test.
        var send = scope.ServiceProvider.GetRequiredService<IEmailService>().SendAsync(Message(), CancellationToken.None);
        await content.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(await send.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, handler.Calls);
        Assert.Equal(1, content.Reads);
        Assert.True(content.Disposed);
        foreach (var entry in logger.Entries)
        {
            Assert.Null(entry.Exception);
            Assert.DoesNotContain("012345", entry.Message);
        }
    }

    /// <summary>
    /// Keeps a caller cancellation during response buffering distinct from an unconfirmed provider outcome.
    /// </summary>
    /// <returns>The original cancellation and single-attempt assertions.</returns>
    [Fact]
    public async Task CallerCancellationDuringBodyBufferingPropagates()
    {
        using var content = new StalledContent();
        using var handler = new ProviderHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = content
        }));
        using var provider = CreateServices(handler).BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var send = scope.ServiceProvider.GetRequiredService<IEmailService>().SendAsync(Message(), cancellation.Token);
        await content.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);
        Assert.Equal(1, content.Reads);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>
    /// Configures the real SDK through production DI while replacing only its network transport.
    /// </summary>
    /// <param name="handler">The deterministic external-provider boundary.</param>
    /// <param name="timeout">The outer native timeout, including response buffering.</param>
    /// <param name="configure">Optional native configuration overrides applied before registration.</param>
    /// <returns>The unbuilt container for additional test-specific dependencies.</returns>
    private static IServiceCollection CreateServices(
        HttpMessageHandler handler,
        string timeout = "00:00:30",
        Action<IConfiguration>? configure = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Email:ApiKey"] = "test-only-key",
            ["Email:FromEmail"] = "sender@example.test",
            ["Email:RequestTimeout"] = timeout
        }).Build();
        configure?.Invoke(configuration);
        var services = new ServiceCollection().AddLogging();
        services.AddSendGridEmail(configuration.GetSection("Email"));
        services.AddSendGridEmail(configuration.GetSection("Email"));
        services.AddHttpClient(CoreConstants.EmailVariables.HTTP_CLIENT_NAME).ConfigurePrimaryHttpMessageHandler(() => handler);

        return services;
    }

    /// <summary>
    /// Supplies synthetic secret content that must not enter transport diagnostics.
    /// </summary>
    /// <returns>The local-only email payload.</returns>
    private static EmailRequestDto Message() => new("owner@example.test", "Reset", "012345", "<p>012345</p>");

    /// <summary>
    /// Counts actual provider attempts while leaving SDK serialization and HTTP buffering intact.
    /// </summary>
    /// <param name="send">The selected provider behavior.</param>
    private sealed class ProviderHandler(Func<CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        /// <summary>
        /// Gets the number of requests that reached the provider boundary.
        /// </summary>
        public int Calls { get; private set; }

        /// <summary>
        /// Returns only headers and content; HttpClient remains responsible for buffering.
        /// </summary>
        /// <param name="request">The SDK request.</param>
        /// <param name="cancellationToken">The effective native cancellation token.</param>
        /// <returns>The scripted provider response.</returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;

            return send(cancellationToken);
        }
    }

    /// <summary>
    /// Simulates a provider that sends headers but never completes its body.
    /// </summary>
    private sealed class StalledContent : HttpContent
    {
        /// <summary>
        /// Signals that buffering has started outside the resilience handler.
        /// </summary>
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Gets how many times buffering requested the body.
        /// </summary>
        public int Reads { get; private set; }

        /// <summary>
        /// Gets whether native HTTP failure cleanup released the content.
        /// </summary>
        public bool Disposed { get; private set; }

        /// <summary>
        /// Supports the base content contract through the cancellation-aware implementation.
        /// </summary>
        /// <param name="stream">The native buffering destination.</param>
        /// <param name="context">The optional transport context.</param>
        /// <returns>The pending body write.</returns>
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        /// <summary>
        /// Stalls until the native caller or HTTP timeout cancels buffering.
        /// </summary>
        /// <param name="stream">The native buffering destination.</param>
        /// <param name="context">The optional transport context.</param>
        /// <param name="cancellationToken">The outer HttpClient buffering token.</param>
        /// <returns>A task that ends only through cancellation.</returns>
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            Reads++;
            Started.TrySetResult();

            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        /// <summary>
        /// Leaves the streamed body length unknown.
        /// </summary>
        /// <param name="length">The unused length.</param>
        /// <returns>False because headers do not announce a complete body length.</returns>
        protected override bool TryComputeLength(out long length)
        {
            length = 0;

            return false;
        }

        /// <summary>
        /// Observes native failure cleanup without adding another body read.
        /// </summary>
        /// <param name="disposing">Whether managed resources are being released.</param>
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
