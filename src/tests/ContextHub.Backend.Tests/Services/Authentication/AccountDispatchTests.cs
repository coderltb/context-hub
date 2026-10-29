namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Checks cancellation ownership at account dispatch without invoking native credentials or database IO.
/// </summary>
public sealed class AccountDispatchTests
{
    /// <summary>
    /// Stops cancelled form requests in the shared pipeline before either account operation is called.
    /// </summary>
    /// <param name="invitation">Whether to dispatch invitation acceptance rather than password login.</param>
    /// <returns>The cancellation and untouched account-service assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledDispatchDoesNotEnterAccountService(bool invitation)
    {
        var accountService = new RecordingAccountService();
        using var provider = CreateProvider(accountService);
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            if (invitation)
            {
                await mediator.Send(new AcceptInvitationCommand(new(Guid.NewGuid(), "proof", "password")), cancellation.Token);
            }
            else
            {
                await mediator.Send(new LoginCommand(new LoginRequestDto { UserName = "owner", Password = "password" }), cancellation.Token);
            }
        });

        Assert.Equal(0, accountService.Calls);
    }

    /// <summary>
    /// Passes the original invitation and token through Mediator to the account IO boundary.
    /// </summary>
    /// <returns>The unchanged DTO, token and successful-outcome assertions.</returns>
    [Fact]
    public async Task InvitationDispatchForwardsRequestAndCancellation()
    {
        var accountService = new RecordingAccountService();
        using var provider = CreateProvider(accountService);
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var request = new AcceptInvitationRequestDto(Guid.NewGuid(), "proof", "password");

        var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new AcceptInvitationCommand(request), cancellation.Token);

        Assert.True(result);
        Assert.Same(request, accountService.Invitation);
        Assert.Equal(cancellation.Token, accountService.Cancellation);
        Assert.Equal(1, accountService.Calls);
    }

    /// <summary>
    /// Retains the Login checkpoint because the native account port has no cancellation parameter.
    /// </summary>
    /// <returns>The handler-boundary cancellation and untouched credential-operation assertions.</returns>
    [Fact]
    public async Task LoginCheckpointPreventsNativeCredentialHandling()
    {
        var accountService = new RecordingAccountService();
        var handler = new LoginCommandHandler(accountService);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await handler.Handle(new LoginCommand(new LoginRequestDto { UserName = "owner", Password = "password" }), cancellation.Token));

        Assert.Equal(0, accountService.Calls);
    }

    /// <summary>
    /// Uses real shared behaviors and generated dispatch while replacing only the native account boundary.
    /// </summary>
    /// <param name="accountService">The recorder owning no external resources.</param>
    /// <returns>The scope-validating service provider.</returns>
    private static ServiceProvider CreateProvider(RecordingAccountService accountService)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorizationBehavior().AddValidationBehavior();
        services.AddApplication();
        services.AddSingleton<IAccountService>(accountService);
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        services.AddOptions<CurrentUserOptions>();
        services.AddScoped<IAuthService, AuthService>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>
    /// Observes dispatch arguments without handling passwords, invitation proof or cookies.
    /// </summary>
    private sealed class RecordingAccountService : IAccountService
    {
        /// <summary>
        /// Gets the number of account operations reached by dispatch.
        /// </summary>
        public int Calls { get; private set; }

        /// <summary>
        /// Gets the original invitation DTO for identity-preservation assertions.
        /// </summary>
        public AcceptInvitationRequestDto? Invitation { get; private set; }

        /// <summary>
        /// Gets the cancellation token forwarded to the cancellable account operation.
        /// </summary>
        public CancellationToken Cancellation { get; private set; }

        /// <summary>
        /// Records entry into the native login boundary without accepting a cancellation token it cannot use.
        /// </summary>
        /// <param name="request">The submitted login DTO; no value is logged.</param>
        /// <returns>A successful test outcome.</returns>
        public Task<bool> LoginAsync(LoginRequestDto request)
        {
            Calls++;

            return Task.FromResult(true);
        }

        /// <summary>
        /// Records the exact request and cancellation token forwarded to persistence.
        /// </summary>
        /// <param name="request">The invitation DTO whose identity must be preserved.</param>
        /// <param name="cancellationToken">The request cancellation token.</param>
        /// <returns>A successful test outcome.</returns>
        public Task<bool> AcceptInvitationAsync(AcceptInvitationRequestDto request, CancellationToken cancellationToken)
        {
            Calls++;
            Invitation = request;
            Cancellation = cancellationToken;

            return Task.FromResult(true);
        }
    }
}
