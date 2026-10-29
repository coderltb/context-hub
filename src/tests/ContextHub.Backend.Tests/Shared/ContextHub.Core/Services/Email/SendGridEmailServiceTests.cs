namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Services.Email;

/// <summary>
/// Exercises the actual SendGrid SDK against a local HTTP handler without sending email or replacing SDK serialization.
/// </summary>
public sealed class SendGridEmailServiceTests
{
    /// <summary>
    /// Resolves shared mail through Core DI while preserving the host-owned native client and sender section.
    /// </summary>
    /// <returns>The configured transport, single-registration and acceptance assertions.</returns>
    [Fact]
    public async Task CoreRegistrationUsesHostSectionAndNativeClient()
    {
        using var handler = new MailHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)));
        using var http = new HttpClient(handler);
        var sdk = new SendGridClient(http, new SendGridClientOptions { ApiKey = "unit-test-only" });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notifications:Email:FromEmail"] = "sender@example.test",
            ["Notifications:Email:FromName"] = "Shared sender"
        }).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<ISendGridClient>(sdk);
        services.AddSendGridEmail(configuration.GetSection("Notifications:Email"));
        services.AddSendGridEmail(configuration.GetSection("Notifications:Email"));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        using var scope = provider.CreateScope();

        Assert.Same(sdk, provider.GetRequiredService<ISendGridClient>());
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IHttpClientFactory));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IEmailService));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ISendGridClient));
        Assert.True(await scope.ServiceProvider.GetRequiredService<IEmailService>().SendAsync(CreateMessage(), CancellationToken.None));
        Assert.Contains("sender@example.test", handler.Body);
        Assert.Contains("Shared sender", handler.Body);
    }

    /// <summary>
    /// Keeps provider acceptance distinct from delivery and never retries rejected transactional sends.
    /// </summary>
    /// <param name="status">The provider HTTP outcome.</param>
    /// <param name="accepted">Whether that status means provider acceptance.</param>
    /// <returns>The request serialization, single-send and safe-log assertions.</returns>
    [Theory]
    [InlineData(202, true)]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(429, false)]
    [InlineData(500, false)]
    public async Task NativeSdkSendsSeparateAlternativesAndLogsOnlyOutcome(int status, bool accepted)
    {
        using var handler = new MailHandler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        {
            Content = new StringContent("sensitive-provider-body password=do-not-log")
        }));
        using var http = new HttpClient(handler);
        var client = new SendGridClient(http, new SendGridClientOptions { ApiKey = "test-only-api-key" });
        var logger = new TestLogger<SendGridEmailService>();
        var service = new SendGridEmailService(client, Options.Create(new SendGridOptions
        {
            FromEmail = "sender@example.test",
            FromName = "Account security"
        }), logger);
        var request = new EmailRequestDto("owner@example.test", "Reset password", "Plain code 012345", "<p>HTML code 012345</p>");

        Assert.Equal(accepted, await service.SendAsync(request, CancellationToken.None));
        Assert.Equal(1, handler.Calls);
        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal("sender@example.test", body.RootElement.GetProperty("from").GetProperty("email").GetString());
        Assert.Equal("owner@example.test", body.RootElement.GetProperty("personalizations")[0].GetProperty("to")[0]
            .GetProperty("email").GetString());
        var alternatives = body.RootElement.GetProperty("content").EnumerateArray().ToArray();
        Assert.Contains(alternatives, value => value.GetProperty("type").GetString() == "text/plain" &&
            value.GetProperty("value").GetString() == request.PlainText);
        Assert.Contains(alternatives, value => value.GetProperty("type").GetString() == "text/html" &&
            value.GetProperty("value").GetString() == request.Html);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(accepted ? LogLevel.Information : LogLevel.Warning, entry.Level);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain("012345", entry.Message);
        Assert.DoesNotContain("owner@example.test", entry.Message);
        Assert.DoesNotContain("do-not-log", entry.Message);
        Assert.DoesNotContain("test-only-api-key", entry.Message);
    }

    /// <summary>
    /// Converts only the known mail transport failure into unsuccessful provider acceptance.
    /// </summary>
    /// <returns>The safe transport outcome and log assertions.</returns>
    [Fact]
    public async Task TransportFailureDoesNotPublishItsMessage()
    {
        var failure = new HttpRequestException("password=never-publish");
        using var handler = new MailHandler(_ => Task.FromException<HttpResponseMessage>(failure));
        using var http = new HttpClient(handler);
        var logger = new TestLogger<SendGridEmailService>();
        var service = CreateService(http, logger);

        Assert.False(await service.SendAsync(CreateMessage(), CancellationToken.None));
        var entry = Assert.Single(logger.Entries);
        Assert.Null(entry.Exception);
        Assert.DoesNotContain("never-publish", entry.Message);
        Assert.Equal(1, handler.Calls);
    }

    /// <summary>
    /// Treats an SDK timeout as failed acceptance while preserving explicit caller cancellation.
    /// </summary>
    /// <returns>The timeout/cancellation and no-retry assertions.</returns>
    [Fact]
    public async Task TimeoutIsUnsuccessfulButCallerCancellationPropagates()
    {
        using var handler = new MailHandler(_ => Task.FromException<HttpResponseMessage>(new TaskCanceledException()));
        using var http = new HttpClient(handler);
        var service = CreateService(http, new TestLogger<SendGridEmailService>());
        Assert.False(await service.SendAsync(CreateMessage(), CancellationToken.None));

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SendAsync(CreateMessage(), cancellation.Token));
    }

    /// <summary>
    /// Keeps unrelated SDK configuration/programming failures outside transport fallback.
    /// </summary>
    /// <returns>The original exception-identity assertion.</returns>
    [Fact]
    public async Task ProgrammingFailurePropagatesWithoutFailureLogging()
    {
        var failure = new InvalidOperationException("programming failure");
        using var handler = new MailHandler(_ => Task.FromException<HttpResponseMessage>(failure));
        using var http = new HttpClient(handler);
        var logger = new TestLogger<SendGridEmailService>();
        var service = CreateService(http, logger);

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendAsync(CreateMessage(), CancellationToken.None)));
        Assert.Empty(logger.Entries);
    }

    /// <summary>
    /// Keeps response cleanup outside send-failure translation after the provider has accepted the message.
    /// </summary>
    /// <returns>The native cleanup exception and acceptance diagnostic assertions.</returns>
    [Fact]
    public async Task AcceptedResponseCleanupIsNotMisclassifiedAsTransportFailure()
    {
        var failure = new HttpRequestException("private-cleanup-message");
        using var handler = new MailHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new CleanupFailureContent(failure)
        }));
        using var http = new HttpClient(handler);
        var logger = new TestLogger<SendGridEmailService>();
        var service = CreateService(http, logger);

        Assert.Same(failure, await Record.ExceptionAsync(() => service.SendAsync(CreateMessage(), CancellationToken.None)));
        Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Information);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    /// <summary>
    /// Injects only native content disposal failure after successful SDK response buffering.
    /// </summary>
    /// <param name="failure">The cleanup error whose identity must remain observable.</param>
    private sealed class CleanupFailureContent(Exception failure) : StringContent("accepted")
    {
        /// <summary>
        /// Releases content before reporting the scripted cleanup error.
        /// </summary>
        /// <param name="disposing">Whether the resource is explicitly disposed.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                throw failure;
            }
        }
    }

    /// <summary>
    /// Creates the real SDK client with a test-owned transport and non-secret sender options.
    /// </summary>
    /// <param name="http">The locally controlled HTTP client.</param>
    /// <param name="logger">The safe capture logger.</param>
    /// <returns>The production email adapter.</returns>
    private static SendGridEmailService CreateService(HttpClient http, TestLogger<SendGridEmailService> logger) =>
        new(new SendGridClient(http, new SendGridClientOptions { ApiKey = "test-only-api-key" }),
            Options.Create(new SendGridOptions { FromEmail = "sender@example.test" }), logger);

    /// <summary>
    /// Supplies one test message that must never enter diagnostics.
    /// </summary>
    /// <returns>The typed transactional message.</returns>
    private static EmailRequestDto CreateMessage() => new("owner@example.test", "Reset", "012345", "<p>012345</p>");

    /// <summary>
    /// Observes the SDK wire payload and controls only its external transport boundary.
    /// </summary>
    /// <param name="send">The scripted provider outcome.</param>
    private sealed class MailHandler(Func<CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        /// <summary>
        /// Gets the number of actual SDK HTTP attempts.
        /// </summary>
        public int Calls { get; private set; }

        /// <summary>
        /// Gets the captured test-only wire payload without logging it.
        /// </summary>
        public string? Body { get; private set; }

        /// <summary>
        /// Captures the request and returns or throws the selected external-provider outcome.
        /// </summary>
        /// <param name="request">The native SDK request.</param>
        /// <param name="cancellationToken">The caller token passed by HttpClient.</param>
        /// <returns>The scripted response.</returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);

            return await send(cancellationToken);
        }
    }
}
