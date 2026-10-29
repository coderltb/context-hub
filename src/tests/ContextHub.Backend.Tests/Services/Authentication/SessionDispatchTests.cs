namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Exercises real Mediator dispatch and shared behaviors while substituting only native authorization persistence.
/// </summary>
public sealed class SessionDispatchTests
{
    /// <summary>
    /// Rejects an Owner that is no longer permitted before protocol grant persistence is reached.
    /// </summary>
    /// <param name="permitted">Whether the current store still permits the Owner.</param>
    /// <returns>The rejection and no-persistence assertions.</returns>
    [Theory]
    [InlineData(false)]
    public async Task UnpermittedSessionsDoNotCreateGrant(bool permitted)
    {
        var sessions = new RecordingSessionService { Permitted = permitted };
        using var provider = CreateProvider(sessions);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new CreateAuthorizationGrantCommand(CreateRequest()));

        Assert.Equal(AuthorizationGrantDecision.Rejected, result.Decision);
        Assert.Null(result.Principal);
        Assert.Equal(0, sessions.Created);
    }

    /// <summary>
    /// Allows a password-authenticated Owner without requiring proof from an authenticator application.
    /// </summary>
    /// <returns>The real Mediator grant decision for the selected password-only cookie.</returns>
    [Fact]
    public async Task PasswordOnlySessionCanCreateGrant()
    {
        var sessions = new RecordingSessionService();
        using var provider = CreateProvider(sessions);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new CreateAuthorizationGrantCommand(CreateRequest()));

        Assert.Equal(AuthorizationGrantDecision.Granted, result.Decision);
        Assert.Same(sessions.Principal, result.Principal);
        Assert.Equal(1, sessions.Created);
    }

    /// <summary>
    /// Fails closed for missing family binding or a protocol rejection before any authorization is created.
    /// </summary>
    /// <param name="sessionId">The trusted cookie's session binding.</param>
    /// <param name="protocolAllowed">Whether native adapter checks accepted the request.</param>
    /// <returns>The native-persistence isolation assertions.</returns>
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("browser-a", false)]
    public async Task InvalidGrantDoesNotReachPersistence(string? sessionId, bool protocolAllowed)
    {
        var sessions = new RecordingSessionService();
        using var provider = CreateProvider(sessions, sessionId: sessionId);
        using var scope = provider.CreateScope();
        var request = CreateRequest() with { ProtocolRequirementsSatisfied = protocolAllowed };

        var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new CreateAuthorizationGrantCommand(request));

        Assert.Equal(AuthorizationGrantDecision.Rejected, result.Decision);
        Assert.Null(result.Principal);
        Assert.Equal(0, sessions.Created);
    }

    /// <summary>
    /// Returns the exact approved principal and passes cancellation through real Mediator without requiring an Admin Bearer policy.
    /// </summary>
    /// <returns>The grant and cancellation propagation assertions.</returns>
    [Fact]
    public async Task AcceptedGrantKeepsPrincipalAndCancellation()
    {
        var sessions = new RecordingSessionService();
        using var provider = CreateProvider(sessions);
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();

        var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new CreateAuthorizationGrantCommand(CreateRequest()), cancellation.Token);

        Assert.Equal(AuthorizationGrantDecision.Granted, result.Decision);
        Assert.Same(sessions.Principal, result.Principal);
        Assert.Equal(cancellation.Token, sessions.Cancellation);
        Assert.Equal(1, sessions.Created);
    }

    /// <summary>
    /// Rechecks current rights for code/refresh exchange rather than accepting a previously authenticated subject forever.
    /// </summary>
    /// <param name="permitted">The current account decision.</param>
    /// <returns>The current-state validation assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TokenExchangeUsesCurrentEligibility(bool permitted)
    {
        var sessions = new RecordingSessionService { Permitted = permitted };
        using var provider = CreateProvider(sessions);
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();

        var allowed = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new ValidateTokenGrantQuery(), cancellation.Token);

        Assert.Equal(cancellation.Token, sessions.Cancellation);
        Assert.Equal(permitted, allowed);
        Assert.Equal(0, sessions.Created);
    }

    /// <summary>
    /// Carries only one protected session pair to native revocation and never creates another authorization.
    /// </summary>
    /// <returns>The exact-family dispatch assertions.</returns>
    [Fact]
    public async Task LogoutSelectsOnlyCurrentSession()
    {
        var sessions = new RecordingSessionService();
        using var provider = CreateProvider(sessions);
        using var scope = provider.CreateScope();
        var session = new SessionRevocationModel("authenticated-subject", "browser-a");

        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new RevokeSessionCommand());

        Assert.Equal(session, sessions.Revoked);
        Assert.Equal(0, sessions.Created);
    }

    /// <summary>
    /// Stops a cancelled request in the existing shared behavior before any current-state or persistence access.
    /// </summary>
    /// <param name="operation">The session request whose shared dispatch must stop before IO.</param>
    /// <returns>The cancellation and untouched collaborator assertions.</returns>
    [Theory]
    [InlineData("authorize")]
    [InlineData("exchange")]
    [InlineData("logout")]
    public async Task CancellationDoesNotEnterSessionService(string operation)
    {
        var sessions = new RecordingSessionService();
        using var provider = CreateProvider(sessions);
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            switch (operation)
            {
                case "authorize":
                    await mediator.Send(new CreateAuthorizationGrantCommand(CreateRequest()), cancellation.Token);
                    break;
                case "exchange":
                    await mediator.Send(new ValidateTokenGrantQuery(), cancellation.Token);
                    break;
                case "logout":
                    await mediator.Send(new RevokeSessionCommand(), cancellation.Token);
                    break;
            }
        });

        Assert.Equal(0, sessions.Lookups);
        Assert.Equal(0, sessions.Created);
        Assert.Null(sessions.Revoked);
    }

    /// <summary>
    /// Uses only the cookie subject for authorization and only the protocol subject for token exchange.
    /// </summary>
    /// <returns>The distinct native claim-source assertions through real Mediator.</returns>
    [Fact]
    public async Task HandlersKeepCookieAndProtocolSubjectsSeparate()
    {
        var sessions = new RecordingSessionService();
        using var provider = CreateProvider(sessions, cookieSubject: "cookie-owner", tokenSubject: "token-owner");
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        Assert.Equal(AuthorizationGrantDecision.Granted,
            (await mediator.Send(new CreateAuthorizationGrantCommand(CreateRequest()))).Decision);
        Assert.Equal("cookie-owner", sessions.Subject);
        Assert.True(await mediator.Send(new ValidateTokenGrantQuery()));
        Assert.Equal("token-owner", sessions.Subject);
    }

    /// <summary>
    /// A protocol subject cannot substitute for a missing Identity cookie subject when issuing an authorization grant.
    /// </summary>
    /// <returns>The sign-in and untouched persistence assertions.</returns>
    [Fact]
    public async Task MissingCookieSubjectRequestsSignInWithoutFallback()
    {
        var sessions = new RecordingSessionService();
        using var provider = CreateProvider(sessions, cookieSubject: null);
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new CreateAuthorizationGrantCommand(CreateRequest()));

        Assert.Equal(AuthorizationGrantDecision.RequiresSignIn, result.Decision);
        Assert.Equal(0, sessions.Lookups);
        Assert.Equal(0, sessions.Created);
    }

    /// <summary>
    /// A cookie subject cannot authorize token redemption when the selected protocol subject is absent.
    /// </summary>
    /// <returns>The rejected token-exchange assertions.</returns>
    [Fact]
    public async Task MissingProtocolSubjectRejectsWithoutFallback()
    {
        var sessions = new RecordingSessionService();
        using var provider = CreateProvider(sessions, tokenSubject: null);
        using var scope = provider.CreateScope();

        Assert.False(await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ValidateTokenGrantQuery()));
        Assert.Equal(0, sessions.Lookups);
    }

    /// <summary>
    /// Logout cannot revoke another family when its selected cookie lacks the current session binding.
    /// </summary>
    /// <param name="sessionId">The absent or empty native family identifier.</param>
    /// <returns>The untouched revocation assertions.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task LogoutWithoutSessionBindingDoesNotRevoke(string? sessionId)
    {
        var sessions = new RecordingSessionService();
        using var provider = CreateProvider(sessions, sessionId: sessionId);
        using var scope = provider.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new RevokeSessionCommand());

        Assert.Null(sessions.Revoked);
    }

    /// <summary>
    /// Runs filter selection and real Mediator dispatch together so an unrelated ambient identity cannot issue the grant.
    /// </summary>
    /// <returns>The end-to-end request-boundary selection assertions without protocol-store substitution in Application.</returns>
    [Fact]
    public async Task FilterSelectedCookieFlowsThroughMediatorAccessor()
    {
        var sessions = new RecordingSessionService();
        var selected = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "selected-cookie"),
            new Claim(AuthenticationConstants.SESSION_ID, "selected-browser"),
            new Claim(AuthenticationConstants.AUTHENTICATION_METHOD_CLAIM, AuthenticationConstants.PASSWORD_AUTHENTICATION_METHOD)
        ], "native-cookie"));
        using var provider = CreateProvider(sessions, cookieSubject: "ambient-owner", nativePrincipal: selected);
        using var scope = provider.CreateScope();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext!;
        http.RequestServices = scope.ServiceProvider;
        var context = new ActionExecutingContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()), [], new Dictionary<string, object?>(), new object());

        await new OidcAuthenticationFilter(OidcAuthenticationOperation.Authorize).OnActionExecutionAsync(context, async () =>
        {
            var result = await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new CreateAuthorizationGrantCommand(CreateRequest()));
            Assert.Equal(AuthorizationGrantDecision.Granted, result.Decision);
            return new ActionExecutedContext(context, [], context.Controller);
        });

        Assert.Equal("selected-cookie", sessions.Subject);
        Assert.Equal(1, sessions.Created);
    }

    /// <summary>
    /// Supplies an internal snapshot rather than binding a subject or session from a consumer request body.
    /// </summary>
    /// <returns>The complete protocol-approved test input.</returns>
    private static AuthorizationGrantRequestModel CreateRequest() =>
        new("context-hub-admin", ["openid", "contexthub.admin"], true);

    /// <summary>
    /// Uses production Mediator and policies while replacing only the port's external stateful operations.
    /// </summary>
    /// <param name="sessions">The test-owned authorization boundary.</param>
    /// <param name="sessionId">The selected cookie's session binding.</param>
    /// <param name="cookieSubject">The native Identity subject, independently absent or different from the token subject.</param>
    /// <param name="tokenSubject">The native protocol subject.</param>
    /// <param name="nativePrincipal">The optional preauthenticated native result for filter-to-Mediator integration.</param>
    /// <returns>The scoped provider; no database is opened.</returns>
    private static ServiceProvider CreateProvider(
        RecordingSessionService sessions,
        string? sessionId = "browser-a",
        string? cookieSubject = "authenticated-subject",
        string? tokenSubject = "authenticated-subject",
        ClaimsPrincipal? nativePrincipal = null)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorizationBehavior().AddValidationBehavior();
        services.AddApplication();
        services.AddSingleton<ISessionService>(sessions);
        var identity = new ClaimsIdentity("selected-native-scheme");
        if (cookieSubject is not null)
        {
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, cookieSubject));
        }

        if (tokenSubject is not null)
        {
            identity.AddClaim(new Claim("sub", tokenSubject));
        }
        identity.AddClaim(new Claim(AuthenticationConstants.AUTHENTICATION_METHOD_CLAIM,
            AuthenticationConstants.PASSWORD_AUTHENTICATION_METHOD));
        if (sessionId is not null)
        {
            identity.AddClaim(new Claim(AuthenticationConstants.SESSION_ID, sessionId));
        }

        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        });
        if (nativePrincipal is not null)
        {
            services.AddSingleton<IAuthenticationService>(new OidcAuthenticationFilterTests.SelectedAuthenticationService(nativePrincipal));
        }

        services.AddOptions<CurrentUserOptions>();
        services.AddScoped<IAuthService, AuthService>();

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>
    /// Records only safe call metadata for the native persistence boundary.
    /// </summary>
    private sealed class RecordingSessionService : ISessionService
    {
        /// <summary>
        /// Gets or sets current account eligibility independently from stale cookie claims.
        /// </summary>
        public bool Permitted { get; init; } = true;

        /// <summary>
        /// Gets the exact principal supplied by native grant construction.
        /// </summary>
        public ClaimsPrincipal Principal { get; } = new(new ClaimsIdentity("native-test"));

        /// <summary>
        /// Gets the number of current-state reads.
        /// </summary>
        public int Lookups { get; private set; }

        /// <summary>
        /// Gets the selected subject to expose accidental cross-scheme claim fallback.
        /// </summary>
        public string? Subject { get; private set; }

        /// <summary>
        /// Gets the number of persisted authorization requests.
        /// </summary>
        public int Created { get; private set; }

        /// <summary>
        /// Gets the selected revocation pair without storing protocol credentials.
        /// </summary>
        public SessionRevocationModel? Revoked { get; private set; }

        /// <summary>
        /// Gets the cancellation token passed to the native boundary.
        /// </summary>
        public CancellationToken Cancellation { get; private set; }

        /// <summary>
        /// Returns the configured current eligibility and observes cancellation propagation.
        /// </summary>
        /// <param name="subject">The authenticated subject.</param>
        /// <param name="cancellationToken">The request cancellation token.</param>
        /// <returns>The permitted model, or null for a rejected account.</returns>
        public Task<UserAccountModel?> GetPermittedUserAsync(string? subject, CancellationToken cancellationToken)
        {
            Lookups++;
            Subject = subject;
            Cancellation = cancellationToken;

            return Task.FromResult(Permitted ? new UserAccountModel(Guid.Empty, "owner") : null);
        }

        /// <summary>
        /// Keeps session state independent from the focused dispatch fixture's account and protocol decisions.
        /// </summary>
        /// <param name="subject">The selected cookie subject.</param>
        /// <param name="sessionId">The selected protected session.</param>
        /// <param name="cancellationToken">The forwarded cancellation token.</param>
        /// <returns>The active fixture session.</returns>
        public Task<bool> IsSessionActiveAsync(string? subject, string? sessionId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        /// <summary>
        /// Records one grant persistence request without generating protocol credentials.
        /// </summary>
        /// <param name="user">The use-case-approved account.</param>
        /// <param name="request">The trusted protocol metadata.</param>
        /// <param name="cancellationToken">The request cancellation token.</param>
        /// <returns>The exact native-boundary principal.</returns>
        public Task<ClaimsPrincipal?> CreatePrincipalAsync(
            UserAccountModel user,
            AuthorizationGrantModel request,
            CancellationToken cancellationToken)
        {
            Created++;
            Cancellation = cancellationToken;

            return Task.FromResult<ClaimsPrincipal?>(Principal);
        }

        /// <summary>
        /// Rejects account-wide revocation so a single-session logout cannot silently broaden its ownership.
        /// </summary>
        /// <param name="id">The human account that must remain outside this test's logout operation.</param>
        /// <param name="cancellationToken">The unused operation cancellation.</param>
        /// <returns>A failed task if logout attempts to revoke another session.</returns>
        public Task RevokeAllAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException("Single-session logout must not revoke all account grants."));

        /// <summary>
        /// Observes exactly the session family selected by Application.
        /// </summary>
        /// <param name="session">The protected subject/session pair.</param>
        /// <param name="cancellationToken">The request cancellation token.</param>
        /// <returns>The completed test revocation.</returns>
        public Task RevokeSessionAsync(SessionRevocationModel session, CancellationToken cancellationToken)
        {
            Revoked = session;
            Cancellation = cancellationToken;

            return Task.CompletedTask;
        }
    }
}
