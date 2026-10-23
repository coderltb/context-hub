namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.DependencyInjection;

/// <summary>
/// Exercises shared composition through startup mapping, DI-managed serialization, transport and authorization.
/// </summary>
[Collection(nameof(Mappings.GlobalMappingCollection))]
public sealed class CoreInfrastructureTests
{
    /// <summary>
    /// Initializes discovered profiles before consumers run even when REST was registered separately first.
    /// </summary>
    [Fact]
    public void RootRegistration_AppliesLoadedProfilesAfterRestServices()
    {
        var services = new ServiceCollection().AddLogging().AddRestClients();

        services.AddCoreInfrastructure();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        var result = new Mappings.GlobalMappingSource { Name = "sample" }.Adapt<Mappings.GlobalMappingTarget>();

        Assert.Equal("sample", result.DisplayName);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IMapper));
    }

    /// <summary>
    /// Discovers only pure, public, concrete profiles with public parameterless constructors.
    /// </summary>
    [Fact]
    public void MappingProfiles_DoNotDependOnContainerOrRequestState()
    {
        var profiles = new[] { typeof(CoreServiceRegistration).Assembly, typeof(CoreInfrastructureTests).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => typeof(IRegister).IsAssignableFrom(type) && type.IsClass && !type.IsAbstract)
            .ToArray();

        Assert.NotEmpty(profiles);
        Assert.All(profiles, type =>
        {
            Assert.True(type.IsVisible);
            Assert.False(type.ContainsGenericParameters);
            Assert.NotNull(type.GetConstructor(Type.EmptyTypes));
        });
    }

    /// <summary>
    /// One root registration supplies the dependencies needed to map and send a JSON request through a custom handler.
    /// </summary>
    /// <returns>The asynchronous transport and serialization assertions.</returns>
    [Fact]
    public async Task RootRegistration_MapsSerializesAndSendsThroughHandler()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddCoreInfrastructure();

        string? body = null;
        services.AddHttpClient("test", client => client.BaseAddress = new Uri("https://example.invalid"))
            .ConfigurePrimaryHttpMessageHandler(() => new CaptureHandler(async request =>
            {
                body = await request.Content!.ReadAsStringAsync();

                return new HttpResponseMessage(HttpStatusCode.Accepted);
            }));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        using var scope = provider.CreateScope();

        var mapped = new Mappings.GlobalMappingSource { Name = "sample" }.Adapt<Mappings.GlobalMappingTarget>();
        var json = scope.ServiceProvider.GetRequiredService<IJsonSerializerService>().Serialize(mapped);
        using var response = await scope.ServiceProvider.GetRequiredService<IRestClientMultipleService>()
            .PostAsync(new BaseHttpRequest { HttpClientName = "test", RequestUri = "/work", RequestData = json });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("{\"displayName\":\"sample\"}", body);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IMapper));
    }

    /// <summary>
    /// A missing policy stops a protected request before either validation or handler execution.
    /// </summary>
    /// <returns>The asynchronous fail-closed pipeline assertions.</returns>
    [Fact]
    public async Task RootRegistration_AuthorizesBeforeValidation()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddCoreInfrastructure();
        services.AddAuthorizationBehavior().AddValidationBehavior();
        var validator = new RecordingValidator();
        services.AddSingleton<IValidator<ProtectedRequest>>(validator);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        using var scope = provider.CreateScope();
        var behaviors = scope.ServiceProvider.GetServices<IPipelineBehavior<ProtectedRequest, int>>().ToArray();
        var handlerCalled = false;
        MessageHandlerDelegate<ProtectedRequest, int> pipeline = (_, _) =>
        {
            handlerCalled = true;
            return ValueTask.FromResult(17);
        };
        foreach (var behavior in behaviors.Reverse())
        {
            var next = pipeline;
            pipeline = (request, token) => behavior.Handle(request, next, token);
        }

        await Assert.ThrowsAsync<CoreAuthorizationException>(async () =>
            await pipeline(new ProtectedRequest(), CancellationToken.None));

        Assert.Collection(behaviors,
            item => Assert.IsType<AuthorizationBehavior<ProtectedRequest, int>>(item),
            item => Assert.IsType<ValidationBehavior<ProtectedRequest, int>>(item));
        Assert.False(validator.Called);
        Assert.False(handlerCalled);
    }

    /// <summary>
    /// Marks the request that must never bypass a missing authorization policy.
    /// </summary>
    private sealed record ProtectedRequest : IRequest<int>, IRequiresAuthorization;

    /// <summary>
    /// Records validation only if authorization allows the pipeline to proceed.
    /// </summary>
    private sealed class RecordingValidator : AbstractValidator<ProtectedRequest>
    {
        /// <summary>
        /// Gets whether the validation rule ran.
        /// </summary>
        public bool Called { get; private set; }

        /// <summary>
        /// Adds a rule that makes unintended validation observable.
        /// </summary>
        public RecordingValidator()
        {
            RuleFor(request => request).Must(_ =>
            {
                Called = true;
                return true;
            });
        }
    }

    /// <summary>
    /// Replaces only the external network boundary, leaving mapping and HTTP content handling real.
    /// </summary>
    /// <param name="send">The response callback observing the outgoing content.</param>
    private sealed class CaptureHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        /// <summary>
        /// Produces the deterministic upstream response without reaching an external service.
        /// </summary>
        /// <param name="request">The actual request created by the shared transport.</param>
        /// <param name="cancellationToken">Cancellation supplied by the HTTP pipeline.</param>
        /// <returns>The callback response.</returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return send(request);
        }
    }
}
