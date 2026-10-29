namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Services.Http;

/// <summary>
/// Exercises the reference DTO facade with real DI, Mapster, Newtonsoft and a native handler boundary.
/// </summary>
[Collection(nameof(GlobalMappingCollection))]
public sealed class ThirdPartyApiServiceTests
{
    /// <summary>
    /// Applies the same fragment redaction to successful facade responses and their existing diagnostic logs.
    /// </summary>
    /// <returns>The asynchronous facade and log assertions.</returns>
    [Fact]
    public async Task SuccessfulEmbeddedJsonResponse_MasksLogsAndReturnedContent()
    {
        var content = "Provider replied: " + JsonSerializer.Serialize(new
        {
            password = "fixture-secret",
            result = "accepted",
            endpoint = "https://fixture-user:uri-secret@example.test/path?api%5Fkey=query-secret&q=visible",
            content = JsonSerializer.Serialize("{\"password\":\"encoded-secret\",\"safe\":\"visible\"}")
        }) + " done";
        var logger = new TestLogger<ThirdPartyApiService>();
        var services = new ServiceCollection().AddLogging().AddCoreInfrastructure();
        services.AddSingleton<ILogger<ThirdPartyApiService>>(logger);
        services.AddHttpClientInfrastructure("provider", _ => { })
            .ConfigurePrimaryHttpMessageHandler(() => new BoundaryHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) })));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var output = await scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>()
            .HandleApiData(new IntegrationRequest { HttpClientName = "provider", Endpoint = "https://selected.invalid" });

        Assert.Contains("accepted", output);
        Assert.DoesNotContain("fixture-secret", output);
        Assert.DoesNotContain("uri-secret", output);
        Assert.DoesNotContain("query-secret", output);
        Assert.DoesNotContain("encoded-secret", output);
        Assert.Contains("/path", output);
        Assert.All(logger.Entries, entry => Assert.DoesNotContain("-secret", entry.Message));
    }

    /// <summary>
    /// Returns the same masked successful body that is logged while retaining non-secret provider content.
    /// </summary>
    /// <returns>The asynchronous facade response and diagnostic assertions.</returns>
    [Fact]
    public async Task SuccessfulTextResponse_MasksAuthorizationAndQuotedSecrets()
    {
        const string content = "Authorization: Bearer bearer-secret; Authorization: Basic basic-secret; password='quoted secret'; path=/reports";
        var logger = new TestLogger<ThirdPartyApiService>();
        var services = new ServiceCollection().AddLogging().AddCoreInfrastructure();
        services.AddSingleton<ILogger<ThirdPartyApiService>>(logger);
        services.AddHttpClientInfrastructure("provider", _ => { })
            .ConfigurePrimaryHttpMessageHandler(() => new BoundaryHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) })));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var request = new IntegrationRequest { HttpClientName = "provider", Endpoint = "https://selected.invalid" };

        var output = await scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>().HandleApiData(request);

        Assert.Equal("Authorization: ***MASKED***; Authorization: ***MASKED***; password='***MASKED***'; path=/reports", output);
        foreach (var entry in logger.Entries)
        {
            Assert.DoesNotContain("bearer-secret", entry.Message);
            Assert.DoesNotContain("basic-secret", entry.Message);
            Assert.DoesNotContain("quoted secret", entry.Message);
        }
    }

    /// <summary>
    /// Keeps useful request and response diagnostics while redacting nested secrets before ILogger sees them.
    /// </summary>
    /// <param name="status">The synthetic upstream status selecting success or failure logging.</param>
    /// <returns>A task that completes after the real facade and its log boundary are verified.</returns>
    [Theory]
    [InlineData(200)]
    [InlineData(409)]
    public async Task Diagnostics_MaskRequestAndResponseBeforeLogging(int status)
    {
        // Keep the HTTP, mapping and serialization implementation real; replace only the remote provider.
        const string responseJson = "{\"result\":\"response-marker\",\"items\":[{\"access_token\":\"response-secret\"}]}";
        var logger = new TestLogger<ThirdPartyApiService>();
        var services = new ServiceCollection().AddLogging().AddCoreInfrastructure();
        services.AddSingleton<ILogger<ThirdPartyApiService>>(logger);
        services.AddHttpClientInfrastructure("provider", _ => { })
            .ConfigurePrimaryHttpMessageHandler(() => new BoundaryHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(responseJson) })));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dto = new IntegrationRequest
        {
            HttpClientName = "provider",
            Endpoint = "https://selected.invalid/private-endpoint?token=url-secret",
            Method = "POST",
            Content = "{\"name\":\"request-marker\",\"password\":\"request-secret\"}",
            AuthenticationValue = new AuthenticationHeaderValue("Bearer", "auth-secret"),
            Headers = new Dictionary<string, string> { ["X-API-Key"] = "header-secret" },
            QueryParameters = new Dictionary<string, string> { ["access_token"] = "query-secret" }
        };
        var service = scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>();

        // Successful results and diagnostic copies both redact credentials; HTTP errors retain their original status.
        if (status == 200)
        {
            var result = await service.HandleApiData(dto);

            Assert.Contains("response-marker", result);
            Assert.DoesNotContain("response-secret", result);
            Assert.Contains("***MASKED***", result);
        }
        else
        {
            var error = await Assert.ThrowsAsync<HttpRequestException>(() => service.HandleApiData(dto));
            Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
            Assert.DoesNotContain("response-secret", error.Message);
            Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
            Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("body read failed", StringComparison.Ordinal));
        }

        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information && entry.Message.Contains("request-marker", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Level == (status == 200 ? LogLevel.Information : LogLevel.Error) &&
                                                entry.Message.Contains("response-marker", StringComparison.Ordinal));
        foreach (var entry in logger.Entries)
        {
            Assert.DoesNotContain("-secret", entry.Message);
            Assert.Null(entry.Exception);
        }

        Assert.Contains(logger.Entries, entry => entry.Message.Contains("private-endpoint", StringComparison.Ordinal));
    }

    /// <summary>
    /// Masks named credentials without discarding ordinary text or JSON larger than the old diagnostic budget.
    /// </summary>
    /// <param name="oversized">Whether the fixture exceeds the diagnostic budget instead of being plain text.</param>
    /// <returns>A task that completes after the unchanged payload and fail-closed logs are verified.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Diagnostics_MaskCredentialsWithoutDiscardingOrdinaryContent(bool oversized)
    {
        var content = oversized
            ? "{\"label\":\"" + new string('x', 40_000) + "public-value\",\"password\":\"private-secret\"}"
            : "public text; password=private-secret";
        var logger = new TestLogger<ThirdPartyApiService>();
        var services = new ServiceCollection().AddLogging().AddCoreInfrastructure();
        services.AddSingleton<ILogger<ThirdPartyApiService>>(logger);
        services.AddHttpClientInfrastructure("provider", _ => { })
            .ConfigurePrimaryHttpMessageHandler(() => new BoundaryHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) })));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dto = new IntegrationRequest { HttpClientName = "provider", Endpoint = "https://selected.invalid", Content = content };

        var result = await scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>().HandleApiData(dto);

        Assert.Contains(oversized ? "public-value" : "public text", result);
        Assert.DoesNotContain("private-secret", result);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information && entry.Message.Contains("***MASKED***", StringComparison.Ordinal));
        Assert.All(logger.Entries, entry => Assert.DoesNotContain("private-secret", entry.Message));
    }

    /// <summary>
    /// Honors the DTO's serialization exclusions instead of invoking a getter explicitly marked as private.
    /// </summary>
    /// <returns>A task that completes after logging and sending succeed without invoking an unrelated getter.</returns>
    [Fact]
    public async Task Diagnostics_HonorIgnoredFeatureProperties()
    {
        var logger = new TestLogger<ThirdPartyApiService>();
        var services = new ServiceCollection().AddLogging().AddCoreInfrastructure();
        services.AddSingleton<ILogger<ThirdPartyApiService>>(logger);
        services.AddHttpClientInfrastructure("provider", _ => { })
            .ConfigurePrimaryHttpMessageHandler(() => new BoundaryHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("accepted") })));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>()
            .HandleApiData(new NonSerializableRequest { HttpClientName = "provider", Endpoint = "https://selected.invalid" });

        Assert.Equal("accepted", result);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information);
    }

    /// <summary>
    /// Applies the shared mapping automatically and returns JSON through Newtonsoft aliases.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task TypedResponse_UsesAutomaticMappingAndNewtonsoft()
    {
        string? body = null;
        string? uri = null;
        var services = new ServiceCollection().AddLogging().AddCoreInfrastructure();
        services.AddHttpClientInfrastructure("provider", _ => { })
            .ConfigurePrimaryHttpMessageHandler(() => new BoundaryHandler(async (request, token) =>
            {
                body = await request.Content!.ReadAsStringAsync(token);
                uri = request.RequestUri!.AbsoluteUri;

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"wire_value\":\"returned\"}") };
            }));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var serializer = scope.ServiceProvider.GetRequiredService<IJsonSerializerService>();
        var service = scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>();
        var dto = new IntegrationRequest
        {
            HttpClientName = "provider", BaseUrl = "https://selected.invalid/api/", Endpoint = "items",
            Method = "post", Content = serializer.Serialize(new WirePayload { Value = "sent" }),
            QueryParameters = new Dictionary<string, string> { ["q"] = "a b" }
        };

        var result = await service.HandleApiData<IntegrationRequest, WirePayload>(dto);

        Assert.Equal("{\"wire_value\":\"sent\"}", body);
        Assert.Equal("https://selected.invalid/api/items?q=a%20b", uri);
        Assert.Equal("returned", result!.Value);
        Assert.Equal("items", dto.Endpoint);
    }

    /// <summary>
    /// Keeps non-JSON responses intact and allows native DI-registered handlers to participate.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task StringResponse_PreservesTextAndCustomHandler()
    {
        const string expected = "plain provider text, not JSON";
        var services = new ServiceCollection().AddLogging().AddCoreInfrastructure();
        services.AddTransient<HeaderHandler>();
        services.AddHttpClientInfrastructure("provider", _ => { })
            .AddHttpMessageHandler<HeaderHandler>()
            .ConfigurePrimaryHttpMessageHandler(() => new BoundaryHandler((request, _) =>
            {
                Assert.Equal("present", Assert.Single(request.Headers.GetValues("X-Test-Handler")));

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(expected) });
            }));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>()
            .HandleApiData(new IntegrationRequest { HttpClientName = "provider", Endpoint = "https://selected.invalid" });

        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Keeps the actual failure status without returning or logging a private provider body.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task Failure_RetainsStatusWithoutLeakingProviderContent()
    {
        var services = new ServiceCollection().AddLogging().AddCoreInfrastructure();
        var logger = new TestLogger<ThirdPartyApiService>();
        services.AddSingleton<ILogger<ThirdPartyApiService>>(logger);
        services.AddHttpClientInfrastructure("provider", _ => { })
            .ConfigurePrimaryHttpMessageHandler(() => new BoundaryHandler((_, _) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent("token=private-token") })));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>()
            .HandleApiData(new IntegrationRequest { HttpClientName = "provider", Endpoint = "https://selected.invalid" }));

        Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
        Assert.DoesNotContain("private-token", error.Message);
        Assert.NotEmpty(logger.Entries);
        foreach (var entry in logger.Entries)
        {
            Assert.DoesNotContain("private-token", entry.Message);
            Assert.Null(entry.Exception);
        }
    }

    /// <summary>
    /// Dispatches every supported verb without changing the caller's verb or interpreting the raw response.
    /// </summary>
    /// <param name="method">The HTTP verb used to dispatch this request.</param>
    /// <param name="expected">The expected result asserted by the scenario.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData("get", "GET")]
    [InlineData("post", "POST")]
    [InlineData("put", "PUT")]
    [InlineData("patch", "PATCH")]
    [InlineData("delete", "DELETE")]
    public async Task DynamicRequest_DispatchesSupportedVerb(string method, string expected)
    {
        string? actual = null;
        var services = new ServiceCollection().AddLogging().AddCoreInfrastructure();
        services.AddHttpClientInfrastructure("provider", _ => { })
            .ConfigurePrimaryHttpMessageHandler(() => new BoundaryHandler((request, _) =>
            {
                actual = request.Method.Method;

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict));
            }));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dto = new IntegrationRequest { HttpClientName = "provider", Endpoint = "https://selected.invalid", Method = method };

        using var response = await scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>().HandleDynamicHttpRequest(dto);

        Assert.Equal(expected, actual);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(method, dto.Method);
    }

    /// <summary>
    /// Preserves repeated form fields and upload resources through the Mapster facade without copying them into JSON.
    /// </summary>
    /// <param name="kind">The request content variant selected by this scenario.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData("form")]
    [InlineData("stream")]
    [InlineData("multipart")]
    public async Task RequestContent_PreservesFormAndUploadKinds(string kind)
    {
        string? body = null;
        string? mediaType = null;
        var services = new ServiceCollection().AddLogging().AddCoreInfrastructure();
        services.AddHttpClientInfrastructure("provider", _ => { })
            .ConfigurePrimaryHttpMessageHandler(() => new BoundaryHandler(async (request, token) =>
            {
                body = await request.Content!.ReadAsStringAsync(token);
                mediaType = request.Content.Headers.ContentType!.MediaType;

                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("accepted") };
            }));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("upload-body"));
        using var file = new StringContent("upload-body");
        var dto = new IntegrationRequest
        {
            HttpClientName = "provider", Endpoint = "https://selected.invalid", Method = "POST",
            ContentType = kind switch
            {
                "form" => "application/x-www-form-urlencoded",
                "multipart" => "multipart/form-data",
                _ => "application/octet-stream"
            },
            FormData = [new("name", "first value"), new("name", "second value")],
            ContentStream = kind == "stream" ? stream : null,
            File = kind == "multipart" ? file : null,
            FileName = "upload.txt", FileFieldName = "attachment"
        };

        var result = await scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>().HandleApiData(dto);

        Assert.Equal("accepted", result);
        Assert.Equal(dto.ContentType, mediaType);
        if (kind == "form")
        {
            Assert.Equal("name=first+value&name=second+value", body);
        }
        else
        {
            Assert.Contains("upload-body", body);
        }

        if (kind == "multipart")
        {
            Assert.Contains("attachment", body);
            Assert.Contains("upload.txt", body);
        }

        Assert.Equal(kind != "stream", stream.CanRead);
    }

    /// <summary>
    /// Rejects unsupported verbs without dispatching a network request or echoing private fields.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task UnsupportedMethod_IsRejectedBeforeSending()
    {
        var sends = 0;
        var services = new ServiceCollection().AddLogging().AddCoreInfrastructure();
        services.AddHttpClientInfrastructure("provider", _ => { })
            .ConfigurePrimaryHttpMessageHandler(() => new BoundaryHandler((_, _) =>
            {
                sends++;

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>()
            .HandleApiData(new IntegrationRequest { HttpClientName = "provider", Endpoint = "https://selected.invalid", Method = "TRACE", Content = "private-body" }));

        Assert.Equal(0, sends);
        Assert.DoesNotContain("private-body", error.Message);
    }

    /// <summary>
    /// Propagates caller cancellation through facade and handlers without generating a fake response.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task Cancellation_IsPreservedThroughFacade()
    {
        var services = new ServiceCollection().AddLogging().AddCoreInfrastructure();
        services.AddHttpClientInfrastructure("provider", _ => { })
            .ConfigurePrimaryHttpMessageHandler(() => new BoundaryHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);

                return new HttpResponseMessage(HttpStatusCode.OK);
            }));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var pending = scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>()
            .HandleApiData(new IntegrationRequest { HttpClientName = "provider", Endpoint = "https://selected.invalid" }, cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    /// <summary>
    /// Defines a feature request extending the reusable integration contract.
    /// </summary>
    public sealed class IntegrationRequest : BaseThirdPartyApiRequest
    {
        // The feature reuses every transport member without introducing another request shape.
    }

    /// <summary>
    /// Verifies that serialization honors a feature property's explicit Newtonsoft exclusion.
    /// </summary>
    public sealed class NonSerializableRequest : BaseThirdPartyApiRequest
    {
        /// <summary>
        /// Throws if serialization ignores the JsonIgnore contract and invokes this getter.
        /// </summary>
        [Newtonsoft.Json.JsonIgnore]
        public string PrivateData => throw new InvalidOperationException("Diagnostics must not invoke this getter.");
    }

    /// <summary>
    /// Detects accidental replacement of Newtonsoft with the framework serializer.
    /// </summary>
    public sealed class WirePayload
    {
        /// <summary>
        /// Gets or sets the provider-specific alias.
        /// </summary>
        [Newtonsoft.Json.JsonProperty("wire_value")]
        public string? Value { get; set; }
    }

    /// <summary>
    /// Adds one synthetic header using the native handler pipeline.
    /// </summary>
    public sealed class HeaderHandler : DelegatingHandler
    {
        /// <summary>
        /// Forwards the request after adding independently observable metadata.
        /// </summary>
        /// <param name="request">The native request sent through the configured HTTP handler pipeline.</param>
        /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
        /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Add("X-Test-Handler", "present");

            return base.SendAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// Replaces only the network boundary while preserving real DI and transport behavior.
    /// </summary>
    /// <param name="send">The synthetic upstream invoked with the real outgoing request and cancellation token.</param>
    private sealed class BoundaryHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        /// <summary>
        /// Invokes the synthetic provider with the actual outgoing request.
        /// </summary>
        /// <param name="request">The native request sent through the configured HTTP handler pipeline.</param>
        /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
        /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
