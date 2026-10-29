namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Exercises native scheme selection and the real scoped current-user accessor without a token parsing replacement.
/// </summary>
public sealed class OidcAuthenticationFilterTests
{
    /// <summary>
    /// Replaces an unrelated ambient identity before Application reads its authenticated claims.
    /// </summary>
    /// <param name="operation">The OIDC action selecting a native scheme.</param>
    /// <param name="claimType">The subject mapping owned by that scheme.</param>
    /// <returns>The selected-identity and scheme assertions.</returns>
    [Theory]
    [InlineData(OidcAuthenticationOperation.Authorize, ClaimTypes.NameIdentifier)]
    [InlineData(OidcAuthenticationOperation.Exchange, "sub")]
    [InlineData(OidcAuthenticationOperation.Logout, ClaimTypes.NameIdentifier)]
    public async Task SelectedSchemeReplacesAmbientIdentity(OidcAuthenticationOperation operation, string claimType)
    {
        var selected = new ClaimsPrincipal(new ClaimsIdentity([new Claim(claimType, "selected-b")], "native-selected"));
        using var provider = CreateProvider(selected);
        using var scope = provider.CreateScope();
        var context = CreateContext(scope.ServiceProvider);
        var entered = false;

        await new OidcAuthenticationFilter(operation).OnActionExecutionAsync(context, () =>
        {
            entered = true;
            var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
            Assert.Equal("selected-b", auth.Get<string>(claimType));
            Assert.DoesNotContain(auth.Principal.Claims, claim => claim.Value == "ambient-a");

            return Task.FromResult(new ActionExecutedContext(context, [], context.Controller));
        });

        Assert.True(entered);
        Assert.Null(context.Result);
        Assert.Equal(operation == OidcAuthenticationOperation.Exchange
            ? OpenIddictServerAspNetCoreDefaults.AuthenticationScheme : IdentityConstants.ApplicationScheme,
            provider.GetRequiredService<SelectedAuthenticationService>().Scheme);
    }

    /// <summary>
    /// Rejects failed authentication without keeping the previously authenticated ambient principal.
    /// </summary>
    /// <param name="operation">The selected native action.</param>
    /// <returns>The fail-closed assertions.</returns>
    [Theory]
    [InlineData(OidcAuthenticationOperation.Authorize)]
    [InlineData(OidcAuthenticationOperation.Exchange)]
    public async Task FailedSchemeNeverFallsBackToAmbientUser(OidcAuthenticationOperation operation)
    {
        using var provider = CreateProvider(null);
        using var scope = provider.CreateScope();
        var context = CreateContext(scope.ServiceProvider);
        var entered = false;

        await new OidcAuthenticationFilter(operation).OnActionExecutionAsync(context, () =>
        {
            entered = true;
            return Task.FromResult(new ActionExecutedContext(context, [], context.Controller));
        });

        Assert.False(entered);
        Assert.False(scope.ServiceProvider.GetRequiredService<IAuthService>().IsAuthenticated);
        Assert.IsType(operation == OidcAuthenticationOperation.Authorize ? typeof(ChallengeResult) : typeof(ForbidResult), context.Result);
    }

    /// <summary>
    /// Allows native logout to clear a missing cookie without borrowing another authentication scheme's session.
    /// </summary>
    /// <returns>The anonymous continuation assertions.</returns>
    [Fact]
    public async Task AnonymousLogoutContinuesWithoutAmbientIdentity()
    {
        using var provider = CreateProvider(null);
        using var scope = provider.CreateScope();
        var context = CreateContext(scope.ServiceProvider);
        var entered = false;

        await new OidcAuthenticationFilter(OidcAuthenticationOperation.Logout).OnActionExecutionAsync(context, () =>
        {
            entered = true;
            Assert.False(scope.ServiceProvider.GetRequiredService<IAuthService>().IsAuthenticated);
            return Task.FromResult(new ActionExecutedContext(context, [], context.Controller));
        });

        Assert.True(entered);
    }

    /// <summary>
    /// Keeps the native principal and HttpContext accessor isolated across overlapping requests.
    /// </summary>
    /// <returns>The request-isolation assertions.</returns>
    [Fact]
    public async Task ParallelRequestsKeepSelectedSubjectsIsolated()
    {
        await Task.WhenAll(Enumerable.Range(0, 20).Select(index => Task.Run(async () =>
        {
            var subject = index.ToString();
            using var provider = CreateProvider(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", subject)], "native")));
            using var scope = provider.CreateScope();
            var context = CreateContext(scope.ServiceProvider);

            await new OidcAuthenticationFilter(OidcAuthenticationOperation.Exchange).OnActionExecutionAsync(context, async () =>
            {
                await Task.Yield();
                Assert.Equal(subject, scope.ServiceProvider.GetRequiredService<IAuthService>().Get<string>("sub"));
                return new ActionExecutedContext(context, [], context.Controller);
            });
        })));
    }

    /// <summary>
    /// Supplies the actual current-user implementation and only substitutes the native authentication result boundary.
    /// </summary>
    /// <param name="principal">The principal authenticated by the selected native scheme, or null for failure.</param>
    /// <returns>The scope-validating provider.</returns>
    private static ServiceProvider CreateProvider(ClaimsPrincipal? principal)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddHttpContextAccessor();
        services.AddOptions<CurrentUserOptions>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton(new SelectedAuthenticationService(principal));
        services.AddSingleton<IAuthenticationService>(provider => provider.GetRequiredService<SelectedAuthenticationService>());

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>
    /// Builds the native refresh request and a deliberately different ambient principal.
    /// </summary>
    /// <param name="services">The request-owned services.</param>
    /// <returns>The MVC filter context.</returns>
    private static ActionExecutingContext CreateContext(IServiceProvider services)
    {
        var http = new DefaultHttpContext { RequestServices = services };
        http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "ambient-a")], "ambient"));
        http.Features.Set(new OpenIddictServerAspNetCoreFeature
        {
            Transaction = new OpenIddictServerTransaction
            {
                Request = new OpenIddictRequest { GrantType = OpenIddictConstants.GrantTypes.RefreshToken }
            }
        });
        services.GetRequiredService<IHttpContextAccessor>().HttpContext = http;

        return new ActionExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), [], new Dictionary<string, object?>(), new object());
    }

    /// <summary>
    /// Returns a preauthenticated principal rather than implementing cookie or token validation in tests.
    /// </summary>
    /// <param name="selectedPrincipal">The selected native result.</param>
    internal sealed class SelectedAuthenticationService(ClaimsPrincipal? selectedPrincipal) : IAuthenticationService
    {
        /// <summary>
        /// Gets the native scheme requested by the production filter.
        /// </summary>
        public string? Scheme { get; private set; }

        /// <summary>
        /// Observes native scheme selection and returns the supplied transport boundary result.
        /// </summary>
        /// <param name="context">The current request.</param>
        /// <param name="scheme">The selected native scheme.</param>
        /// <returns>The selected result without parsing a token.</returns>
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            Scheme = scheme;
            return Task.FromResult(selectedPrincipal is null ? AuthenticateResult.NoResult() : AuthenticateResult.Success(new AuthenticationTicket(selectedPrincipal, scheme!)));
        }

        /// <summary>
        /// Leaves challenge execution to native HTTP result tests.
        /// </summary>
        /// <param name="context">The request.</param>
        /// <param name="scheme">The scheme.</param>
        /// <param name="properties">The challenge metadata.</param>
        /// <returns>The completed boundary operation.</returns>
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        /// <summary>
        /// Leaves forbidden response execution to native HTTP result tests.
        /// </summary>
        /// <param name="context">The request.</param>
        /// <param name="scheme">The scheme.</param>
        /// <param name="properties">The response metadata.</param>
        /// <returns>The completed boundary operation.</returns>
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        /// <summary>
        /// Leaves cookie issuance to Identity integration tests.
        /// </summary>
        /// <param name="context">The request.</param>
        /// <param name="scheme">The scheme.</param>
        /// <param name="principal">The native principal.</param>
        /// <param name="properties">The cookie metadata.</param>
        /// <returns>The completed boundary operation.</returns>
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => Task.CompletedTask;

        /// <summary>
        /// Leaves cookie cleanup to native HTTP result tests.
        /// </summary>
        /// <param name="context">The request.</param>
        /// <param name="scheme">The scheme.</param>
        /// <param name="properties">The cookie metadata.</param>
        /// <returns>The completed boundary operation.</returns>
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }
}
