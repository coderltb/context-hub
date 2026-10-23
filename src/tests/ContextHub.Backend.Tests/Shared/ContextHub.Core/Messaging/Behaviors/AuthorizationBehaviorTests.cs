namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Messaging.Behaviors;

/// <summary>
/// Verifies fail-closed protected requests without coupling Core to an HTTP identity.
/// </summary>
public sealed class AuthorizationBehaviorTests
{
    /// <summary>
    /// Allows an explicitly unprotected request to reach its handler without requiring an unrelated policy.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task Handle_PublicRequestWithoutPolicy_PreservesHandlerResult()
    {
        var behavior = new AuthorizationBehavior<PublicRequest, int>([], NullLogger<AuthorizationBehavior<PublicRequest, int>>.Instance);

        var result = await behavior.Handle(new PublicRequest(), (_, _) => ValueTask.FromResult(17), CancellationToken.None);

        Assert.Equal(17, result);
    }

    /// <summary>
    /// Stops evaluating further policies after denial, preventing both subsequent checks and handler execution.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task Handle_MultiplePolicies_StopsAtFirstDenial()
    {
        var accepted = new DecisionPolicy(true);
        var denied = new DecisionPolicy(false);
        var remaining = new DecisionPolicy(true);
        var behavior = new AuthorizationBehavior<ProtectedRequest, int>([accepted, denied, remaining],
            NullLogger<AuthorizationBehavior<ProtectedRequest, int>>.Instance);
        var handlerCalled = false;

        await Assert.ThrowsAsync<CoreAuthorizationException>(async () => await behavior.Handle(new ProtectedRequest(), (_, _) =>
        {
            handlerCalled = true;
            return ValueTask.FromResult(1);
        }, CancellationToken.None));

        Assert.Equal(1, accepted.CallCount);
        Assert.Equal(1, denied.CallCount);
        Assert.Equal(0, remaining.CallCount);
        Assert.False(handlerCalled);
    }

    /// <summary>
    /// Preserves caller cancellation before starting permission checks or business work.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task Handle_CancelledRequest_DoesNotEvaluatePoliciesOrHandler()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var policy = new DecisionPolicy(true);
        var behavior = new AuthorizationBehavior<ProtectedRequest, int>([policy],
            NullLogger<AuthorizationBehavior<ProtectedRequest, int>>.Instance);
        var handlerCalled = false;

        var exception = await Assert.ThrowsAsync<OperationCanceledException>(async () => await behavior.Handle(new ProtectedRequest(), (_, _) =>
        {
            handlerCalled = true;
            return ValueTask.FromResult(1);
        }, cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, policy.CallCount);
        Assert.False(handlerCalled);
    }

    /// <summary>
    /// Blocks a protected request when no service-owned authorization policy was registered.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task Handle_ProtectedRequestWithoutPolicy_DoesNotExecuteHandler()
    {
        var behavior = new AuthorizationBehavior<ProtectedRequest, int>([], NullLogger<AuthorizationBehavior<ProtectedRequest, int>>.Instance);
        var called = false;

        await Assert.ThrowsAsync<CoreAuthorizationException>(async () => await behavior.Handle(new ProtectedRequest(), (_, _) =>
        {
            called = true;
            return ValueTask.FromResult(1);
        }, CancellationToken.None));

        Assert.False(called);
    }

    /// <summary>
    /// Evaluates policies in registration order and stops before the handler when any policy denies access.
    /// </summary>
    /// <param name="allowed">Whether the service policy grants access.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_PolicyDecision_ControlsHandler(bool allowed)
    {
        var policy = new DecisionPolicy(allowed);
        var behavior = new AuthorizationBehavior<ProtectedRequest, int>([policy], NullLogger<AuthorizationBehavior<ProtectedRequest, int>>.Instance);
        var called = false;
        using var cancellation = new CancellationTokenSource();

        async Task RunAsync() => await behavior.Handle(new ProtectedRequest(), (_, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            called = true;
            return ValueTask.FromResult(1);
        }, cancellation.Token);

        if (allowed)
        {
            await RunAsync();
        }
        else
        {
            await Assert.ThrowsAsync<CoreAuthorizationException>(RunAsync);
        }

        Assert.Equal(allowed, called);
        Assert.Equal(cancellation.Token, policy.ReceivedToken);
    }

    /// <summary>
    /// Represents a protected business request, independent of transport metadata.
    /// </summary>
    private sealed record ProtectedRequest : IRequest<int>, IRequiresAuthorization;

    /// <summary>
    /// Represents a request that does not require a service permission policy.
    /// </summary>
    private sealed record PublicRequest : IRequest<int>;

    /// <summary>
    /// Supplies a deterministic service-owned authorization decision.
    /// </summary>
    /// <param name="allowed">The decision to return.</param>
    private sealed class DecisionPolicy(bool allowed) : IRequestAuthorizationPolicy<ProtectedRequest>
    {
        /// <summary>
        /// Gets the token supplied by the pipeline.
        /// </summary>
        public CancellationToken ReceivedToken { get; private set; }

        /// <summary>
        /// Gets the number of evaluations to detect unexpected calls after a denial or cancellation.
        /// </summary>
        public int CallCount { get; private set; }

        /// <summary>
        /// Returns the configured authorization decision without reading an HTTP context.
        /// </summary>
        /// <param name="request">The protected request.</param>
        /// <param name="cancellationToken">The current cancellation token.</param>
        /// <returns>The deterministic decision.</returns>
        public ValueTask<bool> IsAllowedAsync(ProtectedRequest request, CancellationToken cancellationToken)
        {
            CallCount++;
            ReceivedToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(allowed);
        }
    }
}
