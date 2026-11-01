namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Exercises coded feedback through the real authorization, validation and Mediator pipeline.
/// </summary>
public sealed class QualitySignalApplicationTests
{
    /// <summary>
    /// Uses the validated principal and forwards the caller token to both IO phases.
    /// </summary>
    /// <param name="inserted">Whether persistence inserted or merged a review item.</param>
    /// <param name="status">The existing public outcome code.</param>
    /// <returns>The actor, ordering and contract assertions.</returns>
    [Theory]
    [InlineData(true, "accepted")]
    [InlineData(false, "deduplicated")]
    public async Task ValidSubmissionUsesAuthenticatedActorAndExistingOutcome(bool inserted, string status)
    {
        var io = new CountingSignalService { Inserted = inserted, Count = 10 };
        using var services = CreateServices(io);
        using var cancellation = new CancellationTokenSource();

        var response = await services.GetRequiredService<IMediator>()
            .Send(new SubmitQualitySignalCommand(ValidRequest()), cancellation.Token);

        Assert.Equal(status, response.Status);
        Assert.Equal(new[] { "budget", "persist" }, io.Calls);
        Assert.All(io.Clients, client => Assert.Equal(ClientId, client));
        Assert.All(io.Tokens, token => Assert.Equal(cancellation.Token, token));
    }

    /// <summary>
    /// Stops after atomic coordination when the original fixed-window limit has been exceeded.
    /// </summary>
    /// <returns>The rejected write and unchanged budget-call assertions.</returns>
    [Fact]
    public async Task ExceededBudgetDoesNotAttemptPersistence()
    {
        var io = new CountingSignalService { Count = 11 };
        using var services = CreateServices(io);

        await Assert.ThrowsAsync<QualitySignalRateLimitException>(() => services.GetRequiredService<IMediator>()
            .Send(new SubmitQualitySignalCommand(ValidRequest()), CancellationToken.None).AsTask());

        Assert.Equal(new[] { "budget" }, io.Calls);
    }

    /// <summary>
    /// Rejects unsupported codes or cross-category bindings before consuming Redis budget.
    /// </summary>
    /// <param name="signal">The proposed signal category.</param>
    /// <param name="issue">The proposed issue code.</param>
    /// <returns>The neutral failure and zero-IO assertions.</returns>
    [Theory]
    [InlineData("unknown", "no_matching_package")]
    [InlineData("coverage_gap", "instruction_incorrect")]
    [InlineData("package_issue", "instruction_incorrect")]
    public async Task InvalidCodesAreRejectedBeforeAnyIo(string signal, string issue)
    {
        var io = new CountingSignalService();
        using var services = CreateServices(io);
        var request = ValidRequest() with { SignalType = signal, IssueCode = issue };

        await Assert.ThrowsAsync<CoreValidationException>(() => services.GetRequiredService<IMediator>()
            .Send(new SubmitQualitySignalCommand(request), CancellationToken.None).AsTask());

        Assert.Empty(io.Calls);
    }

    /// <summary>
    /// Fails closed before validation and IO for identities without the fixed client capability grant.
    /// </summary>
    /// <param name="identity">The independently invalid principal variant.</param>
    /// <returns>The shared authorization failure and zero-IO assertions.</returns>
    [Theory]
    [InlineData("anonymous")]
    [InlineData("owner")]
    [InlineData("missing-scope")]
    [InlineData("missing-tool")]
    [InlineData("empty-client")]
    public async Task InvalidIdentityCannotSubmitFeedback(string identity)
    {
        var io = new CountingSignalService();
        using var services = CreateServices(io, identity);

        await Assert.ThrowsAsync<CoreAuthorizationException>(() => services.GetRequiredService<IMediator>()
            .Send(new SubmitQualitySignalCommand(ValidRequest()), CancellationToken.None).AsTask());

        Assert.Empty(io.Calls);
    }

    /// <summary>
    /// Honors pre-dispatch cancellation without consuming budget or writing feedback.
    /// </summary>
    /// <returns>The original cancellation token and zero-IO assertions.</returns>
    [Fact]
    public async Task CancelledRequestDoesNotEnterTheHandler()
    {
        var io = new CountingSignalService();
        using var services = CreateServices(io);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => services.GetRequiredService<IMediator>()
            .Send(new SubmitQualitySignalCommand(ValidRequest()), cancellation.Token).AsTask());

        Assert.Equal(cancellation.Token, failure.CancellationToken);
        Assert.Empty(io.Calls);
    }

    /// <summary>
    /// Preserves unexpected persistence failures rather than turning them into successful submission outcomes.
    /// </summary>
    /// <returns>The original exception-instance assertion.</returns>
    [Fact]
    public async Task PersistenceFailureRemainsTheOriginalException()
    {
        var original = new InvalidOperationException("fixture-only-programming-failure");
        var io = new CountingSignalService { PersistenceFailure = original };
        using var services = CreateServices(io);

        var failure = await Record.ExceptionAsync(() => services.GetRequiredService<IMediator>()
            .Send(new SubmitQualitySignalCommand(ValidRequest()), CancellationToken.None).AsTask());

        Assert.Same(original, failure);
        Assert.Equal(new[] { "budget", "persist" }, io.Calls);
    }

    /// <summary>
    /// Preserves programming, schema and cancellation failures at both feature IO boundaries.
    /// </summary>
    /// <param name="budget">Whether the failure occurs during Redis coordination or PostgreSQL persistence.</param>
    /// <returns>The original exception-instance assertions.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IoAdapterDoesNotReclassifyUnrelatedFailures(bool budget)
    {
        using var cancellation = new CancellationTokenSource();
        Exception[] originals =
        [
            new InvalidOperationException("fixture-programming-failure"),
            new PostgresException("fixture-schema-failure", "ERROR", "ERROR", PostgresErrorCodes.UndefinedTable),
            new OperationCanceledException(cancellation.Token)
        ];
        foreach (var original in originals)
        {
            var service = new QualitySignalService(
                FailingPort<IQualitySignalRepository>(original), FailingPort<IAtomicCacheService>(original),
                NullLogger<QualitySignalService>.Instance);

            var failure = await Record.ExceptionAsync(async () =>
            {
                if (budget)
                {
                    await service.IncrementBudgetAsync(ClientId, cancellation.Token);
                }
                else
                {
                    await service.PersistAsync(ValidRequest(), ClientId, cancellation.Token);
                }
            });

            Assert.Same(original, failure);
        }
    }

    /// <summary>
    /// Retains a known provider failure as the cause of the existing dependency exception.
    /// </summary>
    /// <param name="budget">Whether the unavailable dependency is Redis or PostgreSQL.</param>
    /// <returns>The exact translated dependency cause.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IoAdapterTranslatesOnlyKnownDependencyFailures(bool budget)
    {
        Exception original = budget
            ? new RedisTimeoutException("fixture-provider-failure", CommandStatus.Unknown)
            : new NpgsqlException("fixture-provider-failure", new IOException("fixture-connection-reset"));
        var service = new QualitySignalService(
            FailingPort<IQualitySignalRepository>(original), FailingPort<IAtomicCacheService>(original),
            NullLogger<QualitySignalService>.Instance);

        var failure = await Assert.ThrowsAsync<DependencyUnavailableException>(async () =>
        {
            if (budget)
            {
                await service.IncrementBudgetAsync(ClientId, CancellationToken.None);
            }
            else
            {
                await service.PersistAsync(ValidRequest(), ClientId, CancellationToken.None);
            }
        });

        Assert.Same(original, failure.InnerException);
    }

    /// <summary>
    /// Creates an IO seam that raises one independently supplied native failure.
    /// </summary>
    /// <typeparam name="T">The existing IO port.</typeparam>
    /// <param name="failure">The original native or programming exception.</param>
    /// <returns>The faulting port without a provider connection.</returns>
    private static T FailingPort<T>(Exception failure) where T : class
    {
        var port = DispatchProxy.Create<T, FailurePort>();
        ((FailurePort)(object)port).Failure = failure;

        return port;
    }

    /// <summary>
    /// Uses the actual principal accessor and service-owned assembly registrations.
    /// </summary>
    /// <param name="io">The feature IO seam observed by this test.</param>
    /// <param name="identity">The principal variant supplied by introspection.</param>
    /// <returns>The fixture-owned native service container.</returns>
    private static ServiceProvider CreateServices(CountingSignalService io, string identity = "client")
    {
        var claims = new List<Claim>
        {
            new("actor_type", identity == "owner" ? "user" : "client"),
            new("client_id", (identity == "empty-client" ? Guid.Empty : ClientId).ToString())
        };
        if (identity != "missing-scope")
        {
            claims.Add(new("scope", "contexthub.mcp"));
        }

        if (identity != "missing-tool")
        {
            claims.Add(new("tool", "quality_signal_submit"));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, identity == "anonymous" ? null : "fixture"));
        var authService = new HubAuthAccessor(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } },
            Options.Create(new CurrentUserOptions()), NullLogger<HubAuthAccessor>.Instance);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddContextHubApplication();
        services.AddAuthorizationBehavior().AddValidationBehavior();
        services.AddSingleton<IAuthService>(authService);
        services.AddSingleton<IQualitySignalService>(io);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a valid public coverage signal with no package or free-form task content.
    /// </summary>
    /// <returns>The independent request fixture.</returns>
    private static QualitySignalRequestDto ValidRequest() => new()
    {
        SignalType = "coverage_gap",
        IssueCode = "no_matching_package",
        ArtifactType = "code",
        Operation = "review",
        DesiredPackageType = "workflow"
    };

    private static readonly Guid ClientId = Guid.Parse("6ffde809-8876-4e40-b58a-3455c64ee133");

    /// <summary>
    /// Injects failures at the atomic-cache or repository boundary without replacing service handling.
    /// </summary>
    public class FailurePort : DispatchProxy
    {
        /// <summary>
        /// Gets or sets the exception raised by the selected IO operation.
        /// </summary>
        public Exception Failure { get; set; } = new InvalidOperationException("Fixture failure was not configured.");

        /// <summary>
        /// Raises the exact supplied failure so translation and cancellation can be checked independently.
        /// </summary>
        /// <param name="targetMethod">The IO operation selected by the real service.</param>
        /// <param name="args">The approved IO inputs.</param>
        /// <returns>No value because the provider operation fails.</returns>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw Failure;
    }

    /// <summary>
    /// Records only feature IO so authorization and validation remain production implementations.
    /// </summary>
    private sealed class CountingSignalService : IQualitySignalService
    {
        public long Count { get; init; } = 1;
        public bool Inserted { get; init; } = true;
        public Exception? PersistenceFailure { get; init; }
        public List<string> Calls { get; } = [];
        public List<Guid> Clients { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];

        public Task<long> IncrementBudgetAsync(Guid clientId, CancellationToken cancellationToken)
        {
            Calls.Add("budget");
            Clients.Add(clientId);
            Tokens.Add(cancellationToken);
            return Task.FromResult(Count);
        }

        public Task<bool> PersistAsync(QualitySignalRequestDto request, Guid clientId, CancellationToken cancellationToken)
        {
            Calls.Add("persist");
            Clients.Add(clientId);
            Tokens.Add(cancellationToken);
            return PersistenceFailure is null ? Task.FromResult(Inserted) : Task.FromException<bool>(PersistenceFailure);
        }
    }
}
