namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Exercises the native event adapter separately from database-backed lifecycle predicates.
/// </summary>
public sealed class ClientTokenValidationHandlerTests
{
    /// <summary>
    /// Leaves native OAuth tokens and non-introspection endpoints to OpenIddict without accessing client credentials.
    /// </summary>
    /// <param name="introspection">Whether the request targets introspection.</param>
    /// <param name="token">The submitted test credential.</param>
    /// <returns>The asynchronous protocol-boundary assertions.</returns>
    [Theory]
    [InlineData(false, "ct_v1_malformed")]
    [InlineData(true, "native-openiddict-token")]
    public async Task UnrelatedTokensAndEndpointsAreNotHandled(bool introspection, string token)
    {
        var state = new CredentialState();
        using var provider = CreateProvider(state);
        using var scope = provider.CreateScope();
        var context = CreateContext(token, introspection);

        await scope.ServiceProvider.GetRequiredService<ClientTokenValidationHandler>().HandleAsync(context);

        Assert.False(context.IsRejected);
        Assert.Null(context.Principal);
        Assert.Equal(0, state.Lookups);
    }

    /// <summary>
    /// Rejects a malformed credential before database I/O rather than passing its format to another grant.
    /// </summary>
    /// <returns>The asynchronous rejection assertions.</returns>
    [Fact]
    public async Task MalformedClientCredentialIsRejectedBeforePersistence()
    {
        var state = new CredentialState();
        using var provider = CreateProvider(state);
        using var scope = provider.CreateScope();
        var context = CreateContext("ct_v1_malformed");

        await scope.ServiceProvider.GetRequiredService<ClientTokenValidationHandler>().HandleAsync(context);

        Assert.True(context.IsRejected);
        Assert.Equal("invalid_token", context.Error);
        Assert.Equal(0, state.Lookups);
    }

    /// <summary>
    /// Rejects missing lifecycle state and mismatched digests without publishing either to the protocol response.
    /// </summary>
    /// <param name="missing">Whether the repository reports the credential as unavailable.</param>
    /// <returns>The asynchronous credential verification assertions.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnavailableOrMismatchedCredentialsAreRejected(bool missing)
    {
        var state = new CredentialState();
        using var provider = CreateProvider(state);
        using var scope = provider.CreateScope();
        var id = Guid.NewGuid();
        var issued = ClientTokenHelper.Issue(id, new byte[32]);
        state.Credential = missing ? null : new ClientTokenModel { TokenId = id, SecretHash = new byte[32] };
        var context = CreateContext(issued.Token);

        await scope.ServiceProvider.GetRequiredService<ClientTokenValidationHandler>().HandleAsync(context);

        Assert.True(context.IsRejected);
        Assert.Null(context.Principal);
        Assert.Equal(1, state.Lookups);
    }

    /// <summary>
    /// Attaches only application identity and MCP authority after repository state and constant-time verification agree.
    /// </summary>
    /// <returns>The asynchronous native principal assertions.</returns>
    [Fact]
    public async Task VerifiedCredentialCreatesOnlyAClientPrincipal()
    {
        var state = new CredentialState();
        using var provider = CreateProvider(state);
        using var scope = provider.CreateScope();
        var tokenId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var issued = ClientTokenHelper.Issue(tokenId, new byte[32]);
        state.Credential = new ClientTokenModel
        {
            TokenId = tokenId, ClientId = clientId, SecretHash = issued.Digest,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1), AllowedTools = ["catalog_search", "package_get"]
        };
        using var cancellation = new CancellationTokenSource();
        var context = CreateContext(issued.Token);
        context.Transaction.CancellationToken = cancellation.Token;

        await scope.ServiceProvider.GetRequiredService<ClientTokenValidationHandler>().HandleAsync(context);

        var principal = Assert.IsType<ClaimsPrincipal>(context.Principal);
        Assert.False(context.IsRejected);
        Assert.Equal("client:" + clientId.ToString("D"), principal.FindFirstValue("sub"));
        Assert.Equal("client", principal.FindFirstValue("actor_type"));
        Assert.Equal(tokenId.ToString("D"), principal.FindFirstValue("credential_id"));
        Assert.Equal("contexthub.mcp", Assert.Single(principal.GetScopes()));
        Assert.Null(principal.GetExpirationDate());
        Assert.Equal(
            ["catalog_search", "package_get"],
            principal.FindAll("tool").Select(claim => claim.Value).ToArray());
        Assert.False(principal.IsInRole("Owner"));
        Assert.Equal(tokenId, state.RequestedId);
        Assert.Equal(cancellation.Token, state.Cancellation);
        Assert.DoesNotContain(
            principal.Claims,
            claim => claim.Value == issued.Token || claim.Value == Convert.ToBase64String(issued.Digest));
    }

    /// <summary>
    /// Resolves the real client service and native adapter through DI while replacing only the external persistence read.
    /// </summary>
    /// <param name="state">The test-owned persistence outcome and observations.</param>
    /// <returns>The scoped container; no network connection is opened.</returns>
    private static ServiceProvider CreateProvider(CredentialState state)
    {
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        services.AddSingleton(state);
        services.AddSingleton(TimeProvider.System);
        services.Configure<AuthenticationServiceOptions>(options => options.ClientTokenPepper = Convert.ToBase64String(new byte[32]));
        services.AddDbContext<AuthenticationDbContext>(options => options.UseNpgsql("Host=localhost;Database=not_opened;Username=test"));
        services.AddPersistence<AuthenticationDbContext>();
        services.AddScoped<IClientRepository, ValidationRepository>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<ClientTokenValidationHandler>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>
    /// Creates the native event context without executing other OpenIddict stages.
    /// </summary>
    /// <param name="token">The test token to inspect.</param>
    /// <param name="introspection">Whether the native transaction targets introspection.</param>
    /// <returns>The event context; this is not a full HTTP introspection request.</returns>
    private static ClientTokenValidationContext CreateContext(string token, bool introspection = true) => new(
        new OpenIddictServerTransaction
        {
            EndpointType = introspection ? OpenIddictServerEndpointType.Introspection : OpenIddictServerEndpointType.Token
        }) { Token = token };

    /// <summary>
    /// Records only the internal repository result and safe call metadata, never emitting credential data to logs.
    /// </summary>
    private sealed class CredentialState
    {
        /// <summary>
        /// Gets or sets the credential selected by the test's persistence substitute.
        /// </summary>
        public ClientTokenModel? Credential { get; set; }

        /// <summary>
        /// Gets or sets the observed number of persistence reads.
        /// </summary>
        public int Lookups { get; set; }

        /// <summary>
        /// Gets or sets the public identifier passed to persistence.
        /// </summary>
        public Guid RequestedId { get; set; }

        /// <summary>
        /// Gets or sets the cancellation passed to persistence.
        /// </summary>
        public CancellationToken Cancellation { get; set; }
    }

    /// <summary>
    /// Replaces only the lifecycle lookup, leaving other feature operations outside these unit tests.
    /// </summary>
    /// <param name="db">The context that remains unopened.</param>
    /// <param name="unitOfWork">The shared scoped persistence boundary.</param>
    /// <param name="logger">The shared repository logger.</param>
    /// <param name="state">The test-owned persistence outcome.</param>
    private sealed class ValidationRepository(
        AuthenticationDbContext db,
        IUnitOfWork unitOfWork,
        ILogger<ClientRepository> logger,
        CredentialState state)
        : ClientRepository(db, unitOfWork, logger)
    {
        /// <summary>
        /// Returns the prepared persistence outcome and observes native cancellation forwarding.
        /// </summary>
        /// <param name="tokenId">The parsed credential identifier.</param>
        /// <param name="cancellationToken">The native event cancellation.</param>
        /// <returns>The test-owned credential state.</returns>
        public override Task<ClientTokenModel?> GetValidTokenAsync(Guid tokenId, CancellationToken cancellationToken)
        {
            state.Lookups++;
            state.RequestedId = tokenId;
            state.Cancellation = cancellationToken;

            return Task.FromResult(state.Credential);
        }
    }
}
