namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Messaging.Behaviors;

/// <summary>
/// Verifies handler dispatch, aggregated failures, safe logging and cancellation propagation in the validation pipeline.
/// </summary>
public sealed class ValidationBehaviorTests
{
    /// <summary>
    /// Verifies that a request reaches its handler once when no validators are registered.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task HandleCallsHandlerWhenNoValidatorIsRegistered()
    {
        var handlerCalls = 0;
        var behavior = new ValidationBehavior<TestRequest, string>(
            [],
            NullLogger<ValidationBehavior<TestRequest, string>>.Instance);

        var result = await behavior.Handle(
            new TestRequest("valid"),
            (request, _) =>
            {
                handlerCalls++;
                return ValueTask.FromResult($"handled:{request.Name}");
            },
            CancellationToken.None);

        Assert.Equal("handled:valid", result);
        Assert.Equal(1, handlerCalls);
    }

    /// <summary>
    /// Verifies that successful validation dispatches the handler exactly once.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task HandleCallsHandlerOnceWhenAllValidatorsPass()
    {
        var handlerCalls = 0;
        var behavior = new ValidationBehavior<TestRequest, string>(
        [
            new TestRequestValidator(),
            new PassingValidator()
        ],
        NullLogger<ValidationBehavior<TestRequest, string>>.Instance);

        var result = await behavior.Handle(
            new TestRequest("valid"),
            (_, _) =>
            {
                handlerCalls++;
                return ValueTask.FromResult("handled");
            },
            CancellationToken.None);

        Assert.Equal("handled", result);
        Assert.Equal(1, handlerCalls);
    }

    /// <summary>
    /// Verifies that all validation failures retain field, code and message while preventing handler execution.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task HandleAggregatesFailuresAndDoesNotCallHandler()
    {
        var handlerCalls = 0;
        var behavior = new ValidationBehavior<TestRequest, string>(
        [
            new TestRequestValidator(),
            new SecondaryFailingValidator()
        ],
        NullLogger<ValidationBehavior<TestRequest, string>>.Instance);

        var exception = await Assert.ThrowsAsync<CoreValidationException>(
            () => behavior.Handle(
                    new TestRequest(string.Empty),
                    (_, _) =>
                    {
                        handlerCalls++;
                        return ValueTask.FromResult("handled");
                    },
                    CancellationToken.None)
                .AsTask());

        Assert.Equal(0, handlerCalls);
        Assert.Collection(
            exception.Errors.OrderBy(error => error.Code),
            error =>
            {
                Assert.Equal("name_invalid", error.Code);
                Assert.Equal(nameof(TestRequest.Name), error.Field);
                Assert.Equal("Name must be valid.", error.Message);
            },
            error =>
            {
                Assert.Equal("name_required", error.Code);
                Assert.Equal(nameof(TestRequest.Name), error.Field);
                Assert.Equal("Name is required.", error.Message);
            });
    }

    /// <summary>
    /// Verifies that rejection logs include safe request metadata and codes rather than validation messages.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task HandleLogsSafeStructuredMetadataWhenValidationFails()
    {
        var logger = new TestLogger<ValidationBehavior<TestRequest, string>>();
        var behavior = new ValidationBehavior<TestRequest, string>(
            [new TestRequestValidator()],
            logger);

        await Assert.ThrowsAsync<CoreValidationException>(
            () => behavior.Handle(
                    new TestRequest(string.Empty),
                    (_, _) => ValueTask.FromResult("handled"),
                    CancellationToken.None)
                .AsTask());

        var entry = Assert.Single(logger.Entries, item => item.Level == LogLevel.Warning);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(nameof(TestRequest), entry.Message, StringComparison.Ordinal);
        Assert.Contains("name_required", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Name is required.", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the caller's cancellation token reaches both validators and the handler.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task HandlePassesCancellationTokenToValidatorAndHandler()
    {
        using var cancellation = new CancellationTokenSource();
        var observedValidatorToken = CancellationToken.None;
        var observedHandlerToken = CancellationToken.None;
        var behavior = new ValidationBehavior<TestRequest, string>(
        [
            new TokenObservingValidator(token => observedValidatorToken = token)
        ],
        NullLogger<ValidationBehavior<TestRequest, string>>.Instance);

        await behavior.Handle(
            new TestRequest("valid"),
            (_, token) =>
            {
                observedHandlerToken = token;
                return ValueTask.FromResult("handled");
            },
            cancellation.Token);

        Assert.Equal(cancellation.Token, observedValidatorToken);
        Assert.Equal(cancellation.Token, observedHandlerToken);
    }

    /// <summary>
    /// Prevents concurrent access to shared scoped dependencies and isolates mutable validation contexts.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task HandleRunsValidatorsSequentiallyWithIndependentContexts()
    {
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstCompleted = false;
        var secondStarted = false;
        ValidationContext<TestRequest>? firstContext = null;
        ValidationContext<TestRequest>? secondContext = null;
        var behavior = new ValidationBehavior<TestRequest, string>(
        [
            new ControlledValidator(async (context, token) =>
            {
                firstContext = context;
                await releaseFirst.Task.WaitAsync(token);
                firstCompleted = true;
            }),
            new ControlledValidator((context, _) =>
            {
                secondStarted = true;
                secondContext = context;
                Assert.True(firstCompleted);
                return Task.CompletedTask;
            })
        ],
        NullLogger<ValidationBehavior<TestRequest, string>>.Instance);

        var operation = behavior.Handle(new TestRequest("valid"),
            (_, _) => ValueTask.FromResult("handled"), CancellationToken.None).AsTask();

        // The first validator is held at a known boundary, not delayed by a timing-based sleep.
        try
        {
            Assert.NotNull(firstContext);
            Assert.False(secondStarted);
        }
        finally
        {
            releaseFirst.TrySetResult();
            await operation;
        }

        Assert.True(secondStarted);
        Assert.NotNull(secondContext);
        Assert.NotSame(firstContext, secondContext);
    }

    /// <summary>
    /// Stops a cancelled request before dispatch even when no validators are registered.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task HandleCancelledRequestWithoutValidatorsDoesNotCallHandler()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var handlerCalls = 0;
        var behavior = new ValidationBehavior<TestRequest, string>([],
            NullLogger<ValidationBehavior<TestRequest, string>>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => behavior.Handle(new TestRequest("valid"),
            (_, _) =>
            {
                handlerCalls++;
                return ValueTask.FromResult("handled");
            }, cancellation.Token).AsTask());

        Assert.Equal(0, handlerCalls);
    }

    /// <summary>
    /// Supplies a controlled asynchronous validation boundary without replacing production validation logic.
    /// </summary>
    /// <param name="validate">The test operation executed before normal validation.</param>
    private sealed class ControlledValidator(Func<ValidationContext<TestRequest>, CancellationToken, Task> validate)
        : AbstractValidator<TestRequest>
    {
        /// <summary>
        /// Observes validation sequencing and delegates the result to FluentValidation.
        /// </summary>
        /// <param name="context">The context supplied by the behavior.</param>
        /// <param name="cancellation">The request cancellation token.</param>
        /// <returns>The native validation result.</returns>
        public override async Task<FluentValidation.Results.ValidationResult> ValidateAsync(
            ValidationContext<TestRequest> context, CancellationToken cancellation = default)
        {
            await validate(context, cancellation);
            return await base.ValidateAsync(context, cancellation);
        }
    }

    /// <summary>
    /// Provides a minimal named request for validation pipeline tests.
    /// </summary>
    /// <param name="Name">The fixture request value exercised by validation.</param>
    private sealed record TestRequest(string Name) : IRequest<string>;

    /// <summary>
    /// Rejects an empty name with a stable error code and message.
    /// </summary>
    private sealed class TestRequestValidator : AbstractValidator<TestRequest>
    {
        /// <summary>
        /// Configures the required-name validation rule used by pipeline tests.
        /// </summary>
        public TestRequestValidator()
        {
            RuleFor(request => request.Name)
                .NotEmpty()
                .WithErrorCode("name_required")
                .WithMessage("Name is required.");
        }
    }

    /// <summary>
    /// Provides a second validator that accepts the valid test request.
    /// </summary>
    private sealed class PassingValidator : AbstractValidator<TestRequest>
    {
        /// <summary>
        /// Configures a name-length rule that accepts the valid test input.
        /// </summary>
        public PassingValidator()
        {
            RuleFor(request => request.Name).MaximumLength(100);
        }
    }

    /// <summary>
    /// Produces an additional failure to verify aggregation across validators.
    /// </summary>
    private sealed class SecondaryFailingValidator : AbstractValidator<TestRequest>
    {
        /// <summary>
        /// Configures a deterministic failure with a distinct code for aggregation checks.
        /// </summary>
        public SecondaryFailingValidator()
        {
            RuleFor(request => request.Name)
                .Must(_ => false)
                .WithErrorCode("name_invalid")
                .WithMessage("Name must be valid.");
        }
    }

    /// <summary>
    /// Captures the cancellation token supplied to asynchronous validation.
    /// </summary>
    /// <param name="observeToken">The callback observing the validator's cancellation token.</param>
    private sealed class TokenObservingValidator(Action<CancellationToken> observeToken)
        : AbstractValidator<TestRequest>
    {
        /// <summary>
        /// Records the received token before delegating to FluentValidation.
        /// </summary>
        /// <param name="context">The operation context supplied by the framework.</param>
        /// <param name="cancellation">Cancellation supplied to the validator under test.</param>
        /// <returns>The validation result produced for the supplied context.</returns>
        public override Task<FluentValidation.Results.ValidationResult> ValidateAsync(
            ValidationContext<TestRequest> context,
            CancellationToken cancellation = default)
        {
            observeToken(cancellation);
            return base.ValidateAsync(context, cancellation);
        }
    }
}
