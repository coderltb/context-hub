namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Exercises subject binding and validation through the production Mediator pipeline without claiming persistence coverage.
/// </summary>
public sealed class CurrentUserDispatchTests
{
    /// <summary>
    /// Takes the account ID from the authenticated accessor for both profile queries and password changes.
    /// </summary>
    /// <param name="changePassword">Whether to dispatch the credential mutation instead of the profile query.</param>
    /// <returns>The subject, request, response and cancellation propagation assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OperationsUseTrustedSubjectAndForwardCancellation(bool changePassword)
    {
        var actor = Guid.NewGuid();
        using var provider = CreateProvider(actor, out var recording);
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var request = new ChangePasswordRequestDto("old-secret", "new-secret");

        var result = changePassword
            ? await mediator.Send(new ChangePasswordCommand(request), cancellation.Token)
            : await mediator.Send(new GetCurrentUserQuery(), cancellation.Token);

        Assert.Equal(actor, recording.Subject);
        Assert.Equal(cancellation.Token, recording.Cancellation);
        Assert.Equal(changePassword ? nameof(IUserService.ChangePasswordAsync) : nameof(IUserService.GetAsync), recording.Operation);
        Assert.Same(recording.Profile, result.Data);
        Assert.True(result.Success);
        if (changePassword)
        {
            Assert.Same(request, recording.Request);
            Assert.DoesNotContain("old-secret", request.ToString()!);
            Assert.DoesNotContain("new-secret", request.ToString()!);
        }
    }

    /// <summary>
    /// Rejects anonymous dispatch and empty credential proof before the account port is reached.
    /// </summary>
    /// <param name="authenticated">Whether authorization can reach the validator.</param>
    /// <returns>The expected pipeline rejection and no-IO assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedRequestsNeverReachTheAccountPort(bool authenticated)
    {
        using var provider = CreateProvider(authenticated ? Guid.NewGuid() : null, out var recording);
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var command = new ChangePasswordCommand(new("", ""));

        if (authenticated)
        {
            await Assert.ThrowsAsync<CoreValidationException>(async () => await mediator.Send(command));
        }
        else
        {
            await Assert.ThrowsAsync<CoreAuthorizationException>(async () => await mediator.Send(command));
        }

        Assert.Null(recording.Operation);
    }

    /// <summary>
    /// Builds real authorization, validation and dispatch while replacing only the account IO boundary.
    /// </summary>
    /// <param name="actor">The validated human subject, or null for an anonymous caller.</param>
    /// <param name="recording">The test-owned port recorder.</param>
    /// <returns>The scope-validating container.</returns>
    private static ServiceProvider CreateProvider(Guid? actor, out RecordingUserService recording)
    {
        var service = DispatchProxy.Create<IUserService, RecordingUserService>();
        recording = Assert.IsAssignableFrom<RecordingUserService>(service);
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        services.AddAuthorizationBehavior().AddValidationBehavior();
        services.AddApplication();
        services.AddSingleton(service);
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var identity = actor is { } id
            ? new ClaimsIdentity([new Claim("sub", id.ToString()), new Claim("actor_type", "user"),
                new Claim(ClaimTypes.Role, "Owner"), new Claim("scope", "contexthub.admin")], "test")
            : new ClaimsIdentity();
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        return provider;
    }

    /// <summary>
    /// Records the two exercised account-port calls and rejects accidental calls to other account capabilities.
    /// </summary>
    public class RecordingUserService : DispatchProxy
    {
        /// <summary>
        /// Gets the safe account projection returned by this IO substitute.
        /// </summary>
        public UserResponseDto Profile { get; } = new(Guid.NewGuid(), "owner", null, true, ["Owner"], default, default, "version");

        /// <summary>
        /// Gets the executed account-port operation.
        /// </summary>
        public string? Operation { get; private set; }

        /// <summary>
        /// Gets the subject passed by the Application handler.
        /// </summary>
        public Guid Subject { get; private set; }

        /// <summary>
        /// Gets the forwarded credential DTO without logging it.
        /// </summary>
        public ChangePasswordRequestDto? Request { get; private set; }

        /// <summary>
        /// Gets the cancellation supplied by Mediator.
        /// </summary>
        public CancellationToken Cancellation { get; private set; }

        /// <summary>
        /// Records only approved profile/password operations, preserving the production asynchronous contract.
        /// </summary>
        /// <param name="targetMethod">The account-port member invoked by the handler.</param>
        /// <param name="args">The subject, optional credential DTO and caller cancellation.</param>
        /// <returns>The scripted safe profile task.</returns>
        protected override object Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Operation = targetMethod!.Name;
            Subject = (Guid)args![0]!;
            Cancellation = (CancellationToken)args[^1]!;
            Assert.Contains(Operation, new[] { nameof(IUserService.GetAsync), nameof(IUserService.ChangePasswordAsync) });
            if (Operation == nameof(IUserService.ChangePasswordAsync))
            {
                Request = (ChangePasswordRequestDto)args[1]!;
            }

            return Task.FromResult(Profile);
        }
    }
}
