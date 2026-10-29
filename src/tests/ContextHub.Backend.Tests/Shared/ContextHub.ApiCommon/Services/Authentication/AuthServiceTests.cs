namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.Services.Authentication;

/// <summary>
/// Checks trusted-identity filtering and native claims mapping without building an authentication server.
/// </summary>
public sealed class AuthServiceTests
{
    /// <summary>
    /// Keeps request-local identities isolated through scoped DI readers across parallel asynchronous execution.
    /// </summary>
    /// <returns>The asynchronous isolation assertions.</returns>
    [Fact]
    public async Task ParallelRequests_DoNotShareIdentityOrAuditActor()
    {
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        var ids = Enumerable.Range(0, 20).Select(_ => Guid.NewGuid()).ToArray();

        await Task.WhenAll(ids.Select(id => Task.Run(async () =>
        {
            accessor.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", id.ToString())], "test"))
            };
            using var scope = provider.CreateScope();
            var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
            await Task.Yield();

            Assert.Equal(id, auth.UserId);
            Assert.Equal(id, scope.ServiceProvider.GetRequiredService<IAuditActorAccessor>().ActorId);
            Assert.Same(auth, scope.ServiceProvider.GetRequiredService<IAuthService>());
        })));
    }

    /// <summary>
    /// Ignores anonymous claims even when another identity in the same principal is authenticated.
    /// </summary>
    [Fact]
    public void AnonymousAndMixedPrincipals_DoNotGrantUntrustedClaims()
    {
        var anonymous = new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, "Owner")]);
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(anonymous) };
        var service = Create(context);
        Assert.False(service.IsAuthenticated);
        Assert.Null(service.UserId);
        Assert.False(service.HasRole("Owner"));
        Assert.False(service.TryGet<int?>("missing", out _));
        Assert.Throws<CoreAuthenticationException>(service.EnsureAuthenticated);

        context.User.AddIdentity(new ClaimsIdentity([new Claim("scope", "read write read")], "test"));
        Assert.True(service.IsAuthenticated);
        Assert.False(service.HasRole("Owner"));
        Assert.Equal(["read", "write"], service.Scopes());
        Assert.False(service.HasScope("Read"));
    }

    /// <summary>
    /// Honors subject configuration and each identity's native name and role mapping.
    /// </summary>
    [Fact]
    public void AuthenticatedPrincipal_UsesNativeMappingAndScalarConversion()
    {
        var id = Guid.NewGuid();
        var identity = new ClaimsIdentity([new Claim("user_id", id.ToString()), new Claim("display", "Reader"),
            new Claim("permissions", "Owner"), new Claim("email", "reader@example.test"), new Claim("count", "42"),
            new Claim("day", "1")], "test", "display", "permissions");
        var service = Create(new DefaultHttpContext { User = new ClaimsPrincipal(identity) }, "user_id");
        Assert.Equal(id, service.UserId);
        Assert.Equal("Reader", service.UserName);
        Assert.Equal("reader@example.test", service.Email);
        Assert.True(service.HasRole("Owner"));
        Assert.False(service.HasRole("owner"));
        Assert.Equal(["Owner"], service.Roles());
        Assert.Equal(42, service.Get<int>("count"));
        Assert.Equal(7, service.Get("missing", 7));
        Assert.False(service.TryGet<DayOfWeek>("day", out _));
        service.EnsureAuthenticated();
    }

    /// <summary>
    /// Creates only test-local dependencies for the accessor.
    /// </summary>
    /// <param name="context">The test request.</param>
    /// <param name="subject">The configured normalized subject claim.</param>
    /// <returns>The test accessor.</returns>
    private static AuthService Create(HttpContext context, string subject = "sub") =>
        new(new HttpContextAccessor { HttpContext = context }, Options.Create(new CurrentUserOptions { SubjectClaimType = subject }),
            NullLogger<AuthService>.Instance);
}
