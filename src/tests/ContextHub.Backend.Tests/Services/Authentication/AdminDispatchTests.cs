namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Exercises generated Mediator dispatch with the real shared authorization and validation behaviors.
/// </summary>
[Collection(nameof(GlobalMappingCollection))]
public sealed class AdminDispatchTests
{
    /// <summary>
    /// Issues a non-expiring client credential instead of applying the former implicit ninety-day lifetime.
    /// </summary>
    /// <returns>The asynchronous issuance contract assertion.</returns>
    [Fact]
    public async Task ClientTokenHasNoImplicitExpiry()
    {
        using var provider = CreateProvider(new RecordingClientQueries(), Guid.NewGuid());
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new CreateClientCommand(new CreateClientRequestDto("tool-client")));

        Assert.Null(result.Data!.ExpiresAt);
    }

    /// <summary>
    /// Preserves requested expiry through dispatch, mapping and persistence.
    /// </summary>
    /// <returns>The saved metadata and response assertions.</returns>
    [Fact]
    public async Task ClientTokenPreservesRequestedExpiry()
    {
        using var provider = CreateProvider(new RecordingClientQueries(), Guid.NewGuid());
        using var scope = provider.CreateScope();
        var request = JsonSerializer.Deserialize<CreateClientRequestDto>(
            """{"DisplayName":"expiry-client","ExpiresAt":"2030-01-01T12:00:00+02:00"}""")!;

        var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new CreateClientCommand(request));

        var saved = Assert.Single(scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>()
            .ChangeTracker.Entries<AuthenticationClientTokenEntity>()).Entity;
        Assert.Equal(DateTimeOffset.Parse("2030-01-01T10:00:00Z", CultureInfo.InvariantCulture), saved.ExpiresAt);
        Assert.Equal(saved.ExpiresAt, result.Data!.ExpiresAt);
    }

    /// <summary>
    /// Rejects an elapsed explicit expiry before credential generation or database work.
    /// </summary>
    /// <returns>The validation and absent-write assertions.</returns>
    [Fact]
    public async Task ElapsedClientExpiryIsRejectedBeforePersistence()
    {
        using var provider = CreateProvider(new RecordingClientQueries(), Guid.NewGuid());
        using var scope = provider.CreateScope();
        var request = new CreateClientRequestDto("expiry-client") { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1) };

        await Assert.ThrowsAsync<CoreValidationException>(async () =>
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new CreateClientCommand(request)));

        Assert.Equal(0, provider.GetRequiredService<SuppressDatabaseSaveInterceptor>().Calls);
    }

    /// <summary>
    /// Requires the command workflow to stage the application and first credential before one shared save.
    /// </summary>
    /// <returns>The asynchronous Application workflow assertions.</returns>
    [Fact]
    public async Task CreatingClientStagesBothEntitiesBeforeOneSave()
    {
        using var provider = CreateProvider(new RecordingClientQueries(), Guid.NewGuid());
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new CreateClientCommand(new CreateClientRequestDto(" tool-client ")
        {
            AllowedTools = ["catalog_search", "catalog_search", "package_get"]
        }));

        var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        var client = Assert.Single(db.ChangeTracker.Entries<AuthenticationClientEntity>()).Entity;
        var token = Assert.Single(db.ChangeTracker.Entries<AuthenticationClientTokenEntity>()).Entity;
        Assert.Equal("tool-client", client.Name);
        Assert.Same(client, token.Client);
        Assert.Equal(client.CreatedBy, token.CreatedBy);
        Assert.Null(token.ExpiresAt);
        Assert.NotEqual(default, client.CreatedAt);
        Assert.Equal(client.CreatedAt, client.UpdatedAt);
        Assert.Equal(client.CreatedAt, token.CreatedAt);
        Assert.Equal(client.Id, result.Data!.ClientId);
        Assert.Equal(token.Id, result.Data.TokenId);
        Assert.Equal(new[] { "catalog_search", "package_get" }, token.AllowedTools);
        Assert.Equal(token.AllowedTools, result.Data.AllowedTools);
        Assert.NotEmpty(token.SecretHash);
        Assert.NotEmpty(result.Data.Token);
        Assert.Equal(1, provider.GetRequiredService<SuppressDatabaseSaveInterceptor>().Calls);
    }

    /// <summary>
    /// Takes the actor from the authenticated principal and passes cancellation to the owning port.
    /// </summary>
    /// <returns>The asynchronous dispatch assertions.</returns>
    [Fact]
    public async Task AuthorizedCommandUsesPrincipalActorAndForwardsCancellation()
    {
        var actor = Guid.NewGuid();
        var clients = new RecordingClientQueries();
        using var provider = CreateProvider(clients, actor);
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(
            new CreateClientCommand(new CreateClientRequestDto("tool-client")),
            cancellation.Token);

        var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        Assert.Equal(actor, Assert.Single(db.ChangeTracker.Entries<AuthenticationClientEntity>()).Entity.CreatedBy);
        Assert.Equal(cancellation.Token, provider.GetRequiredService<SuppressDatabaseSaveInterceptor>().Cancellation);
        Assert.Equal(1, provider.GetRequiredService<SuppressDatabaseSaveInterceptor>().Calls);
        Assert.True(result.Success);
        Assert.Equal("Bearer", result.Data!.TokenType);
    }

    /// <summary>
    /// Runs authorization before validation and never calls persistence for a rejected principal.
    /// </summary>
    /// <returns>The asynchronous fail-closed assertions.</returns>
    [Fact]
    public async Task UnauthorizedCommandIsRejectedBeforeValidationAndPersistence()
    {
        var clients = new RecordingClientQueries();
        using var provider = CreateProvider(clients, null);
        using var scope = provider.CreateScope();

        // The invalid body would fail validation if an anonymous request reached that stage.
        await Assert.ThrowsAsync<CoreAuthorizationException>(async () =>
            await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new CreateClientCommand(new CreateClientRequestDto(""))));

        Assert.Equal(0, clients.Calls);
    }

    /// <summary>
    /// Rejects invalid metadata using the shared validator rather than writing through Infrastructure.
    /// </summary>
    /// <returns>The asynchronous validation assertions.</returns>
    [Fact]
    public async Task AuthorizedInvalidCommandDoesNotReachPersistence()
    {
        var clients = new RecordingClientQueries();
        using var provider = CreateProvider(clients, Guid.NewGuid());
        using var scope = provider.CreateScope();

        await Assert.ThrowsAsync<CoreValidationException>(async () =>
            await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new CreateClientCommand(new CreateClientRequestDto(""))));

        Assert.Equal(0, clients.Calls);
    }

    /// <summary>
    /// Resolves an actual query handler and keeps the shared pagination contract intact.
    /// </summary>
    /// <returns>The asynchronous query assertions.</returns>
    [Fact]
    public async Task QueryUsesApplicationPortAndSharedPage()
    {
        var clients = new RecordingClientQueries();
        using var provider = CreateProvider(clients, Guid.NewGuid());
        using var scope = provider.CreateScope();
        var request = new GetClientsRequestDto { Page = 2, PageSize = 10 };

        var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new GetClientsQuery(request));

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Page);
        Assert.Equal(10, result.Data.PageSize);
        Assert.Empty(result.Data.Items);
        Assert.Equal(1, clients.Calls);
    }

    /// <summary>
    /// Stops an oversized external page before the handler can call its persistence service port.
    /// </summary>
    /// <returns>The neutral validation failure and untouched service assertions.</returns>
    [Fact]
    public async Task OversizedClientPageDoesNotReachService()
    {
        var clients = new RecordingClientQueries();
        using var provider = CreateProvider(clients, Guid.NewGuid());
        using var scope = provider.CreateScope();

        await Assert.ThrowsAsync<CoreValidationException>(async () =>
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(
                new GetClientsQuery(new() { Page = int.MaxValue, PageSize = 100 })));

        Assert.Equal(0, clients.Calls);
    }

    /// <summary>
    /// Builds only the collaborators needed by these use cases, using the production shared accessor and behaviors.
    /// </summary>
    /// <param name="clients">The recording credential boundary.</param>
    /// <param name="actor">The authenticated human, or null for an anonymous request.</param>
    /// <returns>The scope-validating test container.</returns>
    private static ServiceProvider CreateProvider(RecordingClientQueries clients, Guid? actor)
    {
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        services.AddAuthorizationBehavior().AddValidationBehavior();
        services.AddApplication();
        services.AddSingleton(clients);
        services.AddScoped<IClientRepository, TestClientRepository>();
        services.AddScoped<IClientService, ClientService>();
        services.AddMapping();
        services.AddSingleton(TimeProvider.System);
        services.Configure<AuthenticationServiceOptions>(options => options.ClientTokenPepper = Convert.ToBase64String(new byte[32]));
        services.AddAudit();
        var saving = new SuppressDatabaseSaveInterceptor();
        services.AddSingleton(saving);
        services.AddDbContext<AuthenticationDbContext>((provider, options) => options
            .UseNpgsql("Host=localhost;Database=stage_only;Username=unused;Password=unused")
            .UseOpenIddict<Guid>().AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>(), saving));
        services.AddPersistence<AuthenticationDbContext>();
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var identity = actor is { } id
            ? new ClaimsIdentity(
                [new Claim("sub", id.ToString()), new Claim("actor_type", "user"),
                new Claim(ClaimTypes.Role, "Owner"), new Claim("scope", "contexthub.admin")],
                "test")
            : new ClaimsIdentity();
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        return provider;
    }

    /// <summary>
    /// Suppresses only the database write so this dispatch test can inspect staging without claiming PostgreSQL evidence.
    /// </summary>
    private sealed class SuppressDatabaseSaveInterceptor : SaveChangesInterceptor
    {
        /// <summary>
        /// Counts saves requested by the Application workflow.
        /// </summary>
        public int Calls { get; private set; }

        /// <summary>
        /// Gets the cancellation passed to the final save.
        /// </summary>
        public CancellationToken Cancellation { get; private set; }

        /// <summary>
        /// Records the final shared save and prevents network I/O in this unit-level test.
        /// </summary>
        /// <param name="eventData">The context requesting persistence.</param>
        /// <param name="result">The native save interception decision.</param>
        /// <param name="cancellationToken">The propagated caller cancellation.</param>
        /// <returns>A suppressed save, not a simulated database commit.</returns>
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Cancellation = cancellationToken;

            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(2));
        }
    }

    /// <summary>
    /// Records only the paged-query override while writes exercise the real feature repository.
    /// </summary>
    private sealed class RecordingClientQueries
    {
        /// <summary>
        /// Counts paged queries dispatched through the Application port.
        /// </summary>
        public int Calls { get; set; }
    }

    /// <summary>
    /// Overrides one query while preserving the production repository's shared staging and UoW save behavior.
    /// </summary>
    /// <param name="db">The test context whose save is explicitly suppressed.</param>
    /// <param name="unitOfWork">The real shared save boundary.</param>
    /// <param name="logger">The safe shared repository logger.</param>
    /// <param name="queries">The test-local query observer.</param>
    private sealed class TestClientRepository(
        AuthenticationDbContext db,
        IUnitOfWork unitOfWork,
        ILogger<ClientRepository> logger,
        RecordingClientQueries queries)
        : ClientRepository(db, unitOfWork, logger)
    {
        /// <summary>
        /// Returns an empty page without database I/O, keeping query-handler assertions independent of PostgreSQL.
        /// </summary>
        /// <param name="request">The grouped public list inputs.</param>
        /// <param name="cancellationToken">The caller cancellation.</param>
        /// <returns>The empty requested page.</returns>
        public override Task<PageResponse<ClientResponseDto>> GetPageAsync(
            GetClientsRequestDto request,
            CancellationToken cancellationToken)
        {
            queries.Calls++;

            return Task.FromResult(new PageResponse<ClientResponseDto>(
                [],
                new PageRequest(
                request.Page,
                request.PageSize),
                0));
        }
    }
}
