namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Checks real Identity writes, invitation replay and password-only sessions against an isolated PostgreSQL container.
/// </summary>
[Collection(nameof(GlobalMappingCollection))]
public sealed class OwnerEnrollmentTests : IAsyncLifetime
{
    /// <summary>
    /// Owns only test data; no deployment connection string is read by this fixture.
    /// </summary>
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("authentication_enrollment_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    /// <summary>
    /// Owns the real API registration and scoped native managers without starting external HTTP listeners.
    /// </summary>
    private WebApplication? _app;

    /// <summary>
    /// Injects only the selected revocation write failure after normal native account provisioning.
    /// </summary>
    private readonly SessionCommandFailureInterceptor _sessionFailure = new();

    /// <summary>
    /// Synchronizes only the cold-registration regression at the native role-read boundary.
    /// </summary>
    private readonly ProvisioningReadBarrier _provisioningBarrier = new();

    /// <summary>
    /// Rejects database-existence probes while the fixture creates tables in its container-owned database.
    /// </summary>
    private readonly SchemaCreationInterceptor _schemaCreation = new();

    /// <summary>
    /// Builds the native mapped schema only inside this fixture's fresh Testcontainer, never in a product database.
    /// </summary>
    /// <returns>The isolated database and DI initialization.</returns>
    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ClientsController).Assembly.GetName().Name,
            ContentRootPath = AuthenticationTestHostPaths.GetContentRoot(),
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(AuthenticationTestKeys.Create());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Authentication"] = _database.GetConnectionString(),
            ["Authentication:ClientTokenPepper"] = Convert.ToBase64String(new byte[32]),
            ["Authentication:IntrospectionClientSecret"] = new string('x', 32),
            ["Observability:Logging:MinimumLevel"] = "Fatal"
        });
        builder.Services.AddLoggingInfrastructure(builder.Configuration, "authentication-enrollment-tests", Environments.Development);
        builder.Services.AddAuthenticationApi(builder.Configuration, builder.Environment);
        builder.Services.AddPostgreSqlFixture<AuthenticationDbContext>(_database.GetConnectionString());
        builder.Services.AddDbContext<AuthenticationDbContext>((_, options) => options.AddInterceptors(_sessionFailure, _provisioningBarrier, _schemaCreation));
        _app = builder.Build();
        using var scope = _app.Services.CreateScope();

        // Testcontainers already created the database; create only its mapped tables.
        await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>()
            .GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
    }

    /// <summary>
    /// Creates the mapped schema without an existence probe that could misclassify a reset as an absent database.
    /// </summary>
    /// <returns>The native account write and independent-context schema assertions.</returns>
    [Fact]
    public async Task FixtureCreatesTablesWithoutDatabaseExistenceProbe()
    {
        Assert.False(_schemaCreation.ExistenceProbeObserved);
        Guid id;
        using (var scope = _app!.Services.CreateScope())
        {
            var owner = await scope.ServiceProvider.GetRequiredService<IOwnerRegistrationService>().RegisterAsync(
                new("schema-owner", "schema-owner@example.test", "Valid-test-Password!123"), CancellationToken.None);
            id = owner.Id;
        }

        // Read committed data through a separate scoped context, not the writer's tracker.
        using var observer = _app.Services.CreateScope();
        var saved = await observer.ServiceProvider.GetRequiredService<AuthenticationDbContext>()
            .Users.SingleAsync(user => user.Id == id);

        Assert.Equal("schema-owner@example.test", saved.Email);
        Assert.False(_schemaCreation.ExistenceProbeObserved);
    }

    /// <summary>
    /// Preserves an injected provider DDL error instead of treating failed schema creation as readiness success.
    /// </summary>
    /// <returns>The original provider failure identity and SQLSTATE assertions.</returns>
    [Fact]
    public async Task SchemaCreationPropagatesProviderFailure()
    {
        var failure = new PostgresException("Injected schema permission failure.", "ERROR", "ERROR",
            PostgresErrorCodes.InsufficientPrivilege);
        _schemaCreation.SchemaFailure = failure;
        using var scope = _app!.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();

        var observed = await Assert.ThrowsAsync<PostgresException>(() =>
            context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync());

        Assert.Same(failure, observed);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, observed.SqlState);
    }

    /// <summary>
    /// Keeps the original credential on rejection and changes it through native Identity only after valid proof.
    /// </summary>
    /// <param name="validProof">Whether the submitted current password matches.</param>
    /// <returns>The independent-context password, security-stamp and native account-setting preservation assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CurrentPasswordChangeUsesNativeProofAndPersists(bool validProof)
    {
        Guid id;
        string? originalStamp;
        using (var scope = _app!.Services.CreateScope())
        {
            var profile = await scope.ServiceProvider.GetRequiredService<IOwnerRegistrationService>()
                .RegisterAsync(new("owner", "owner@example.test", "Original-password!123"), CancellationToken.None);
            id = profile.Id;
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = (await userManager.FindByIdAsync(id.ToString()))!;
            user.TwoFactorEnabled = true;
            Assert.True((await userManager.UpdateAsync(user)).Succeeded);
            originalStamp = user.SecurityStamp;
        }

        using (var scope = _app.Services.CreateScope())
        {
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
            var request = new ChangePasswordRequestDto(validProof ? "Original-password!123" : "incorrect-proof", "Replacement-password!456");
            if (validProof)
            {
                Assert.Equal(id, (await userService.ChangePasswordAsync(id, request, CancellationToken.None)).Id);
            }
            else
            {
                await Assert.ThrowsAsync<BusinessException>(() => userService.ChangePasswordAsync(id, request, CancellationToken.None));
            }
        }

        // Verify committed credentials in a separate context, independent of the operation's change tracker.
        using var observer = _app.Services.CreateScope();
        var manager = observer.ServiceProvider.GetRequiredService<UserManager<User>>();
        var saved = (await manager.FindByIdAsync(id.ToString()))!;
        Assert.Equal(validProof, await manager.CheckPasswordAsync(saved, "Replacement-password!456"));
        Assert.Equal(!validProof, await manager.CheckPasswordAsync(saved, "Original-password!123"));
        Assert.True(saved.TwoFactorEnabled);
        Assert.True(await manager.IsInRoleAsync(saved, "Owner"));
        Assert.Equal(!validProof, saved.SecurityStamp == originalStamp);
    }

    /// <summary>
    /// Signs in an active Owner with a password and keeps the native account flag from requiring removed authenticator pages.
    /// </summary>
    /// <param name="legacyTwoFactorEnabled">Whether the native store retains an old two-factor setting.</param>
    /// <returns>The password-only cookie, current grant eligibility and native lockout reset assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PasswordOnlyLoginCreatesSessionWithoutEnrollment(bool legacyTwoFactorEnabled)
    {
        using var scope = _app!.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var owner = await provider.GetRequiredService<IOwnerRegistrationService>().RegisterAsync(
            new("owner", "owner@example.test", "Valid-test-Password!123"), CancellationToken.None);
        var userManager = provider.GetRequiredService<UserManager<User>>();
        var user = (await userManager.FindByIdAsync(owner.Id.ToString()))!;
        if (legacyTwoFactorEnabled)
        {
            Assert.True((await userManager.ResetAuthenticatorKeyAsync(user)).Succeeded);
            Assert.True((await userManager.SetTwoFactorEnabledAsync(user, true)).Succeeded);
        }

        Assert.True((await userManager.AccessFailedAsync(user)).Succeeded);
        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Scheme = "https";
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;

        var result = await provider.GetRequiredService<IAccountService>().LoginAsync(
            new LoginRequestDto { UserName = "owner", Password = "Valid-test-Password!123" });

        Assert.True(result);
        Assert.True(http.Response.Headers.SetCookie.Count > 0);
        Assert.Equal(0, await userManager.GetAccessFailedCountAsync(user));
        Assert.NotNull(await provider.GetRequiredService<IUserRepository>().GetPermittedUserAsync(owner.Id.ToString()));
        Assert.Equal(0, await userManager.CountRecoveryCodesAsync(user));
    }

    /// <summary>
    /// Keeps failed credentials, lockout, disabled users and missing Owner membership from establishing a cookie.
    /// </summary>
    /// <param name="rejection">The independent native-account or credential rejection case.</param>
    /// <returns>The failed sign-in and absent session-cookie assertions.</returns>
    [Theory]
    [InlineData("incorrect-password")]
    [InlineData("locked")]
    [InlineData("inactive")]
    [InlineData("not-owner")]
    public async Task PasswordOnlyLoginRetainsNativeRejections(string rejection)
    {
        using var scope = _app!.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var owner = await provider.GetRequiredService<IOwnerRegistrationService>().RegisterAsync(
            new("owner", "owner@example.test", "Valid-test-Password!123"), CancellationToken.None);
        var userManager = provider.GetRequiredService<UserManager<User>>();
        var user = (await userManager.FindByIdAsync(owner.Id.ToString()))!;
        if (rejection == "inactive")
        {
            user.IsActive = false;
            Assert.True((await userManager.UpdateAsync(user)).Succeeded);
        }
        else if (rejection == "locked")
        {
            Assert.True((await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(5))).Succeeded);
        }
        else if (rejection == "not-owner")
        {
            Assert.True((await userManager.RemoveFromRoleAsync(user, AuthenticationConstants.OWNER)).Succeeded);
        }

        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Scheme = "https";
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
        var password = rejection == "incorrect-password" ? "incorrect" : "Valid-test-Password!123";

        var result = await provider.GetRequiredService<IAccountService>().LoginAsync(
            new LoginRequestDto { UserName = "owner", Password = password });

        Assert.False(result);
        Assert.Equal(0, http.Response.Headers.SetCookie.Count);
    }

    /// <summary>
    /// Posts the real antiforgery-protected login form and resumes a local continuation without an enrollment redirect.
    /// </summary>
    /// <returns>The native Razor password-session response assertions against PostgreSQL.</returns>
    [Fact]
    public async Task RazorPasswordLoginReturnsToLocalContinuation()
    {
        using (var scope = _app!.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IOwnerRegistrationService>().RegisterAsync(
                new("owner", "owner@example.test", "Valid-test-Password!123"), CancellationToken.None);
        }

        _app.UseAuthenticationApi();
        await _app.StartAsync();
        _app.GetTestServer().BaseAddress = new Uri("https://localhost");
        using var client = _app.GetTestClient();
        using var form = await client.GetAsync("/login");
        var html = await form.Content.ReadAsStringAsync();
        Assert.DoesNotContain("second factor", html, StringComparison.OrdinalIgnoreCase);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);
        var cookies = new CookieContainer();
        foreach (var cookie in form.Headers.GetValues("Set-Cookie"))
        {
            cookies.SetCookies(new Uri("https://localhost"), cookie);
        }

        client.DefaultRequestHeaders.Add("Cookie", cookies.GetCookieHeader(new Uri("https://localhost")));
        using var input = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value),
            ["Input.UserName"] = "owner",
            ["Input.Password"] = "Valid-test-Password!123",
            ["ReturnUrl"] = "/"
        });

        using var result = await client.PostAsync("/login", input);

        Assert.Equal(HttpStatusCode.Redirect, result.StatusCode);
        Assert.Equal("/", result.Headers.Location!.OriginalString);
        Assert.Contains(result.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith("__Host-context-hub-sso=", StringComparison.Ordinal));
    }

    /// <summary>
    /// Creates additional Owners even when an earlier account was disabled.
    /// </summary>
    /// <returns>The committed native accounts and memberships.</returns>
    [Fact]
    public async Task OwnerRegistrationAllowsExistingAndInactiveAccounts()
    {
        using var scope = _app!.Services.CreateScope();
        var registration = scope.ServiceProvider.GetRequiredService<IOwnerRegistrationService>();
        var first = await registration.RegisterAsync(new("owner", "owner@example.test", "Valid-test-Password!123"), CancellationToken.None);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = (await userManager.FindByIdAsync(first.Id.ToString()))!;
        user.IsActive = false;
        Assert.True((await userManager.UpdateAsync(user)).Succeeded);

        var second = await registration.RegisterAsync(
            new("another-owner", "another@example.test", "Valid-test-Password!123"), CancellationToken.None);

        Assert.NotEqual(first.Id, second.Id);
        Assert.True(await userManager.IsInRoleAsync((await userManager.FindByIdAsync(second.Id.ToString()))!, "Owner"));
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Users.CountAsync());
    }

    /// <summary>
    /// Accepts a single-use native invitation and permits the active Owner without authenticator enrollment.
    /// </summary>
    /// <returns>The persisted password, single-use invitation and password-only grant assertions.</returns>
    [Fact]
    public async Task InvitationIsSingleUseAndPermitsPasswordOnlyOwner()
    {
        using var scope = _app!.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Scheme = "https";
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
        var service = provider.GetRequiredService<IUserService>();
        await scope.ServiceProvider.GetRequiredService<IOwnerRegistrationService>().RegisterAsync(
            new("owner", "owner@example.test", "Valid-test-Password!123"), CancellationToken.None);
        var first = await service.InviteAsync(new("invited", "invited@example.test", ["Owner"]), CancellationToken.None);
        var invitation = QueryHelpers.ParseQuery(new Uri(first.InvitationUrl).Query)["token"].ToString();
        var accounts = provider.GetRequiredService<IAccountService>();
        const string password = "Valid-test-Password!123";

        Assert.False(await accounts.AcceptInvitationAsync(new(first.User.Id, "invalid-token", password), CancellationToken.None));
        Assert.True(await accounts.AcceptInvitationAsync(new(first.User.Id, invitation, password), CancellationToken.None));
        Assert.False(await accounts.AcceptInvitationAsync(new(first.User.Id, invitation, password), CancellationToken.None));
        var repository = provider.GetRequiredService<IUserRepository>();
        Assert.NotNull(await repository.GetPermittedUserAsync(first.User.Id.ToString()));

        var userManager = provider.GetRequiredService<UserManager<User>>();
        var user = (await userManager.FindByIdAsync(first.User.Id.ToString()))!;
        Assert.False(user.TwoFactorEnabled);
        Assert.Equal(0, await userManager.CountRecoveryCodesAsync(user));
    }

    /// <summary>
    /// Returns a retryable business conflict when two first requests race on missing native role metadata.
    /// </summary>
    /// <returns>The HTTP contract, rollback and explicit-resubmission assertions.</returns>
    [Fact]
    public async Task ColdConcurrentRegistrationsReturnConflictAndAllowResubmission()
    {
        _provisioningBarrier.Enabled = true;
        var app = _app!;
        app.UseAuthenticationApi();
        await app.StartAsync();
        app.GetTestServer().BaseAddress = new Uri("https://localhost");
        using var client = app.GetTestClient();
        var requests = new[]
        {
            new CreateUserRequestDto("first-owner", "first@example.test", "Valid-test-Password!123"),
            new CreateUserRequestDto("second-owner", "second@example.test", "Valid-test-Password!123")
        };

        // Both requests observe the role as absent before either native INSERT can commit.
        var responses = await Task.WhenAll(requests.Select(request => client.PostAsJsonAsync("/api/admin/v1/users", request)));
        using var first = responses[0];
        using var second = responses[1];
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        var conflict = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Contains("error_conflict", await conflict.Content.ReadAsStringAsync());
        using (var observer = app.Services.CreateScope())
        {
            var db = observer.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
            Assert.Equal(1, await db.Users.CountAsync());
            Assert.Equal(1, await db.UserRoles.CountAsync());
            Assert.Equal(1, await db.Roles.CountAsync());
        }

        // Resubmission is caller-owned and uses a fresh request scope after the failed transaction rolled back.
        _provisioningBarrier.Enabled = false;
        var rejected = Array.IndexOf(responses, conflict);
        using var retry = await client.PostAsJsonAsync("/api/admin/v1/users", requests[rejected]);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        using var verification = app.Services.CreateScope();
        var saved = verification.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        Assert.Equal(2, await saved.Users.CountAsync());
        Assert.Equal(2, await saved.UserRoles.CountAsync());
        Assert.Equal(1, await saved.Roles.CountAsync());
    }

    /// <summary>
    /// Classifies native duplicate-role results without reclassifying unrelated Identity validation failures.
    /// </summary>
    /// <param name="duplicateRole">Whether the native validator reports the known provisioning race.</param>
    /// <returns>The business classification and absent partial-account assertions.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NativeProvisioningResultKeepsItsFailureClassification(bool duplicateRole)
    {
        using var scope = _app!.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var error = duplicateRole
            ? new IdentityErrorDescriber().DuplicateRoleName("Owner")
            : new IdentityErrorDescriber().InvalidRoleName("Owner");
        roleManager.RoleValidators.Add(new RejectedRoleValidator(error));

        var failure = await Assert.ThrowsAsync<BusinessException>(() => scope.ServiceProvider
            .GetRequiredService<IOwnerRegistrationService>().RegisterAsync(
                new("owner", "owner@example.test", "Valid-test-Password!123"), CancellationToken.None));

        Assert.Equal(duplicateRole ? BusinessErrorKind.Conflict : BusinessErrorKind.Invalid, failure.Kind);
        using var observer = _app.Services.CreateScope();
        var db = observer.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.Roles.ToListAsync());
    }

    /// <summary>
    /// Preserves unrelated unique violations, other SQL states and cancellation at native role creation.
    /// </summary>
    /// <param name="failureKind">The provider-boundary failure outside the translated role-name conflict.</param>
    /// <returns>The original failure identity and rolled-back database assertions.</returns>
    [Theory]
    [InlineData("other-constraint")]
    [InlineData("other-sqlstate")]
    [InlineData("cancellation")]
    public async Task ProvisioningPreservesUnrelatedProviderFailures(string failureKind)
    {
        Exception original = failureKind switch
        {
            "other-constraint" => new PostgresException("fixture", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation,
                constraintName: "OtherConstraint"),
            "other-sqlstate" => new PostgresException("fixture", "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation,
                constraintName: "RoleNameIndex"),
            _ => new OperationCanceledException(new CancellationToken(canceled: true))
        };
        _provisioningBarrier.RoleInsertFailure = original;
        using var scope = _app!.Services.CreateScope();

        var failure = await Record.ExceptionAsync(() => scope.ServiceProvider.GetRequiredService<IOwnerRegistrationService>()
            .RegisterAsync(new("owner", "owner@example.test", "Valid-test-Password!123"), CancellationToken.None));

        Assert.NotNull(failure);
        Assert.IsNotType<BusinessException>(failure);
        Assert.Same(original, failure.GetBaseException());
        using var observer = _app.Services.CreateScope();
        var db = observer.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.Roles.ToListAsync());
    }

    /// <summary>
    /// Starts independent registrations together against provisioned native role/client records.
    /// </summary>
    /// <returns>The independently committed Owner and membership assertions.</returns>
    [Fact]
    public async Task ConcurrentRegistrationsCanCommitMultipleOwners()
    {
        using (var provisioning = _app!.Services.CreateScope())
        {
            await provisioning.ServiceProvider.GetRequiredService<IIdentityProvisioningService>().ProvisionAsync(CancellationToken.None);
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = new[] { AttemptAsync("first-owner"), AttemptAsync("second-owner") };
        start.SetResult();
        var errors = await Task.WhenAll(attempts);
        Assert.All(errors, Assert.Null);

        using var verification = _app.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        Assert.Equal(2, await db.Users.CountAsync());
        Assert.Equal(2, await db.UserRoles.CountAsync());

        // Each request owns a different scope/context. Native serialization/unique conflicts remain real failures, not retries.
        async Task<Exception?> AttemptAsync(string name)
        {
            using var scope = _app.Services.CreateScope();
            await start.Task;

            return await Record.ExceptionAsync(() => scope.ServiceProvider.GetRequiredService<IOwnerRegistrationService>()
                .RegisterAsync(new(name, name + "@example.test", "Valid-test-Password!123"), CancellationToken.None));
        }
    }

    /// <summary>
    /// Keeps one browser's native authorization/token family independent from another session for the same Owner.
    /// </summary>
    /// <returns>The real PostgreSQL revocation isolation assertions.</returns>
    [Fact]
    public async Task SessionRevocationPreservesOtherAuthorizationFamilies()
    {
        using var scope = _app!.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var first = await provider.GetRequiredService<IOwnerRegistrationService>()
            .RegisterAsync(new("owner", "owner@example.test", "Valid-test-Password!123"), CancellationToken.None);
        var sessions = provider.GetRequiredService<ISessionService>();
        var account = new UserAccountModel(first.Id, first.UserName);
        var request = new AuthorizationGrantModel(
            "browser-a", "context-hub-admin", ["openid", "contexthub.admin"]);

        // This test exercises native storage directly; handler tests separately enforce current Owner eligibility.
        var firstPrincipal = await sessions.CreatePrincipalAsync(account, request, CancellationToken.None);
        var otherPrincipal = await sessions.CreatePrincipalAsync(
            account, request with { SessionId = "browser-b" }, CancellationToken.None);
        Assert.NotNull(firstPrincipal);
        Assert.NotNull(otherPrincipal);
        var tokens = provider.GetRequiredService<IOpenIddictTokenManager>();
        var firstToken = await tokens.CreateAsync(new OpenIddictTokenDescriptor
        {
            AuthorizationId = firstPrincipal.GetAuthorizationId(),
            Subject = account.Id.ToString(),
            Type = OpenIddictConstants.TokenTypeHints.AccessToken,
            Status = OpenIddictConstants.Statuses.Valid,
            CreationDate = DateTimeOffset.UtcNow,
            ExpirationDate = DateTimeOffset.UtcNow.AddMinutes(15)
        });
        var otherToken = await tokens.CreateAsync(new OpenIddictTokenDescriptor
        {
            AuthorizationId = otherPrincipal.GetAuthorizationId(),
            Subject = account.Id.ToString(),
            Type = OpenIddictConstants.TokenTypeHints.AccessToken,
            Status = OpenIddictConstants.Statuses.Valid,
            CreationDate = DateTimeOffset.UtcNow,
            ExpirationDate = DateTimeOffset.UtcNow.AddMinutes(15)
        });

        await sessions.RevokeSessionAsync(new(account.Id.ToString(), "browser-a"), CancellationToken.None);

        // Bulk native updates do not rewrite tracked objects; verify persisted state through independent managers.
        using var verification = _app.Services.CreateScope();
        var authorizations = verification.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var observedTokens = verification.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var firstStoredToken = (await observedTokens.FindByIdAsync((await tokens.GetIdAsync(firstToken))!))!;
        var otherStoredToken = (await observedTokens.FindByIdAsync((await tokens.GetIdAsync(otherToken))!))!;
        var firstAuthorization = (await authorizations.FindByIdAsync(firstPrincipal.GetAuthorizationId()!))!;
        var otherAuthorization = (await authorizations.FindByIdAsync(otherPrincipal.GetAuthorizationId()!))!;
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, await authorizations.GetStatusAsync(firstAuthorization));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await authorizations.GetStatusAsync(otherAuthorization));
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, await observedTokens.GetStatusAsync(firstStoredToken));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await observedTokens.GetStatusAsync(otherStoredToken));
    }

    /// <summary>
    /// Rejects a logged-out session at persistence even when it never obtained a grant and logout is repeated.
    /// </summary>
    /// <returns>The durable marker and independent-session assertions.</returns>
    [Fact]
    public async Task LogoutWithoutGrantIsDurableAndIdempotent()
    {
        using var scope = _app!.Services.CreateScope();
        var owner = await scope.ServiceProvider.GetRequiredService<IOwnerRegistrationService>().RegisterAsync(
            new("owner", "owner@example.test", "Valid-test-Password!123"), CancellationToken.None);
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionService>();
        var account = new UserAccountModel(owner.Id, owner.UserName);
        var session = new SessionRevocationModel(owner.Id.ToString(), "browser-a");
        await sessions.RevokeSessionAsync(session, CancellationToken.None);
        await sessions.RevokeSessionAsync(session, CancellationToken.None);

        Assert.False(await sessions.IsSessionActiveAsync(session.Subject, session.SessionId, CancellationToken.None));
        Assert.Null(await sessions.CreatePrincipalAsync(account,
            new("browser-a", "context-hub-admin", ["openid", "contexthub.admin"]), CancellationToken.None));
        Assert.NotNull(await sessions.CreatePrincipalAsync(account,
            new("browser-b", "context-hub-admin", ["openid", "contexthub.admin"]), CancellationToken.None));
    }

    /// <summary>
    /// Prevents a grant whose marker read overlaps committed logout from restoring that session.
    /// </summary>
    /// <returns>The PostgreSQL serialization failure and durable logout assertions.</returns>
    [Fact]
    public async Task OverlappingGrantCannotRestoreLoggedOutSession()
    {
        UserResponseDto owner;
        using (var setup = _app!.Services.CreateScope())
        {
            owner = await setup.ServiceProvider.GetRequiredService<IOwnerRegistrationService>().RegisterAsync(
                new("owner", "owner@example.test", "Valid-test-Password!123"), CancellationToken.None);
        }

        using var grantScope = _app.Services.CreateScope();
        using var logoutScope = _app.Services.CreateScope();
        _sessionFailure.PauseMarkerRead = true;
        var grant = Record.ExceptionAsync(() => grantScope.ServiceProvider.GetRequiredService<ISessionService>()
            .CreatePrincipalAsync(new(owner.Id, owner.UserName),
                new("browser-a", "context-hub-admin", ["openid", "contexthub.admin"]), CancellationToken.None));
        await _sessionFailure.MarkerRead.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try
        {
            // Commit logout after the old snapshot read but before authorization is written.
            await logoutScope.ServiceProvider.GetRequiredService<ISessionService>()
                .RevokeSessionAsync(new(owner.Id.ToString(), "browser-a"), CancellationToken.None);
        }
        finally
        {
            _sessionFailure.ResumeGrant.TrySetResult();
        }

        var failure = await grant;
        Assert.NotNull(failure);
        Assert.Equal(PostgresErrorCodes.SerializationFailure, Assert.IsType<PostgresException>(failure.GetBaseException()).SqlState);
        using var observer = _app.Services.CreateScope();
        Assert.False(await observer.ServiceProvider.GetRequiredService<ISessionService>()
            .IsSessionActiveAsync(owner.Id.ToString(), "browser-a", CancellationToken.None));
        Assert.False(await observer.ServiceProvider.GetRequiredService<AuthenticationDbContext>()
            .Set<NativeAuthenticationAuthorization>().AnyAsync(value => value.Subject == owner.Id.ToString() && value.Status == "valid"));
    }

    /// <summary>
    /// Requires logout failures to roll back the selected family without reporting false success or masking cancellation.
    /// </summary>
    /// <param name="authorizationFailure">Whether the native authorization update, rather than token bulk update, fails.</param>
    /// <param name="cancel">Whether the failing token command also cancels the caller.</param>
    /// <returns>The failure classification and independent database rollback assertions.</returns>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task SessionRevocationFailureRollsBackTheFamily(bool authorizationFailure, bool cancel)
    {
        Guid ownerId;
        using (var scope = _app!.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            var owner = await provider.GetRequiredService<IOwnerRegistrationService>().RegisterAsync(
                new("owner", "owner@example.test", "Valid-test-Password!123"), CancellationToken.None);
            ownerId = owner.Id;
            var sessions = provider.GetRequiredService<ISessionService>();
            var principal = await sessions.CreatePrincipalAsync(new(owner.Id, owner.UserName),
                new("browser-a", "context-hub-admin", ["openid", "contexthub.admin"]), CancellationToken.None);
            Assert.NotNull(principal);
            await provider.GetRequiredService<IOpenIddictTokenManager>().CreateAsync(new OpenIddictTokenDescriptor
            {
                AuthorizationId = principal.GetAuthorizationId(), Subject = owner.Id.ToString(),
                Type = OpenIddictConstants.TokenTypeHints.RefreshToken, Status = OpenIddictConstants.Statuses.Valid,
                CreationDate = DateTimeOffset.UtcNow, ExpirationDate = DateTimeOffset.UtcNow.AddHours(1)
            });
            using var cancellation = new CancellationTokenSource();
            _sessionFailure.AuthorizationFailure = authorizationFailure;
            _sessionFailure.Cancellation = cancel ? cancellation : null;
            _sessionFailure.Enabled = true;

            if (cancel)
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sessions.RevokeSessionAsync(
                    new(owner.Id.ToString(), "browser-a"), cancellation.Token));
            }
            else
            {
                var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => sessions.RevokeSessionAsync(
                    new(owner.Id.ToString(), "browser-a"), cancellation.Token));
                if (authorizationFailure)
                {
                    // TryRevoke reports a failed native update as false; logout must convert that result into a safe failure.
                    Assert.Equal(AuthenticationErrorConstants.GRANT_REVOCATION_FAILED, failure.Message);
                }
                else
                {
                    Assert.Same(_sessionFailure.Failure, failure);
                }
            }
        }

        // Observe rollback outside the failed context; native stores can retain in-memory changes after a database rollback.
        using var observer = _app.Services.CreateScope();
        var database = observer.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        Assert.True(await observer.ServiceProvider.GetRequiredService<ISessionService>()
            .IsSessionActiveAsync(ownerId.ToString(), "browser-a", CancellationToken.None));
        Assert.Equal("valid", Assert.Single(await database.Set<NativeAuthenticationAuthorization>().AsNoTracking()
            .Where(value => value.Subject == ownerId.ToString()).Select(value => value.Status).ToListAsync()));
        Assert.Equal("valid", Assert.Single(await database.Set<NativeAuthenticationToken>().AsNoTracking()
            .Where(value => value.Subject == ownerId.ToString()).Select(value => value.Status).ToListAsync()));
    }

    /// <summary>
    /// Rebuilds native cookie claims without losing the validated session or retaining stale authorization claims.
    /// </summary>
    /// <param name="authenticationMethod">The original completed method, or null when no method was established.</param>
    /// <returns>The refreshed native-principal allowlist assertions.</returns>
    [Theory]
    [InlineData("pwd")]
    [InlineData(null)]
    public async Task CookieRefreshPreservesOnlySessionAndEstablishedMethod(string? authenticationMethod)
    {
        using var scope = _app!.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var owner = await provider.GetRequiredService<IOwnerRegistrationService>().RegisterAsync(
            new("owner", "owner@example.test", "Valid-test-Password!123"), CancellationToken.None);
        var users = provider.GetRequiredService<UserManager<User>>();
        var user = (await users.FindByIdAsync(owner.Id.ToString()))!;
        var principal = await provider.GetRequiredService<SignInManager<User>>().CreateUserPrincipalAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new Claim(AuthenticationConstants.SESSION_ID, "browser-refresh"));
        identity.AddClaim(new Claim("fixture-unapproved", "must-disappear"));
        identity.AddClaim(new Claim(ClaimTypes.Role, "stale-role"));
        if (authenticationMethod is not null)
        {
            identity.AddClaim(new Claim(AuthenticationConstants.AUTHENTICATION_METHOD_CLAIM, authenticationMethod));
        }

        // Force a native refresh with test-only time; production never receives an injected clock for this feature.
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        provider.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value.TimeProvider = clock;
        var context = CreateCookieValidationContext(provider, principal, clock.GetUtcNow().AddMinutes(-31));
        await provider.GetRequiredService<ISecurityStampValidator>().ValidateAsync(context);

        Assert.NotNull(context.Principal);
        Assert.True(context.ShouldRenew);
        Assert.Equal("browser-refresh", context.Principal.FindFirstValue(AuthenticationConstants.SESSION_ID));
        Assert.Equal(authenticationMethod, context.Principal.FindFirstValue(AuthenticationConstants.AUTHENTICATION_METHOD_CLAIM));
        Assert.Null(context.Principal.FindFirst("fixture-unapproved"));
        Assert.False(context.Principal.IsInRole("stale-role"));
        Assert.True(context.Principal.IsInRole(AuthenticationConstants.OWNER));
    }

    /// <summary>
    /// Rejects a changed native security stamp on the next cookie use rather than waiting for the default thirty-minute interval.
    /// </summary>
    /// <returns>The immediate rejection assertion using a real persisted account.</returns>
    [Fact]
    public async Task CookieWithOldSecurityStampIsRejectedImmediately()
    {
        using var scope = _app!.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var owner = await provider.GetRequiredService<IOwnerRegistrationService>().RegisterAsync(
            new("owner", "owner@example.test", "Valid-test-Password!123"), CancellationToken.None);
        var users = provider.GetRequiredService<UserManager<User>>();
        var user = (await users.FindByIdAsync(owner.Id.ToString()))!;
        var principal = await provider.GetRequiredService<SignInManager<User>>().CreateUserPrincipalAsync(user);
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        provider.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value.TimeProvider = clock;
        var context = CreateCookieValidationContext(provider, principal, clock.GetUtcNow());
        Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded);
        clock.Advance(TimeSpan.FromSeconds(1));

        await provider.GetRequiredService<ISecurityStampValidator>().ValidateAsync(context);

        Assert.Null(context.Principal);
    }

    /// <summary>
    /// Preserves the first revoker when two independent contexts read the same unrevoked credential.
    /// </summary>
    /// <param name="parallel">Whether the stale requests also execute their writes concurrently.</param>
    /// <returns>The idempotent outcomes and committed metadata assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentTokenRevocationPreservesOriginalMetadata(bool parallel)
    {
        var actor = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        using (var setup = _app!.Services.CreateScope())
        {
            var provider = setup.ServiceProvider;
            provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", actor.ToString())], "fixture-owner"))
            };
            var db = provider.GetRequiredService<AuthenticationDbContext>();
            db.Clients.Add(new AuthenticationClientEntity { Id = clientId, Name = "revoke-race" });
            db.ClientTokens.Add(new AuthenticationClientTokenEntity
            {
                Id = tokenId, ClientId = clientId, Prefix = "test-prefix", SecretHash = [1, 2, 3],
                AllowedTools = ["catalog_search"]
            });
            await db.SaveChangesAsync();
        }

        using var first = _app.Services.CreateScope();
        using var second = _app.Services.CreateScope();
        var firstRepository = first.ServiceProvider.GetRequiredService<IClientRepository>();
        var secondRepository = second.ServiceProvider.GetRequiredService<IClientRepository>();
        var firstModel = (await firstRepository.GetTokenAsync(clientId, tokenId, CancellationToken.None))!;
        var secondModel = (await secondRepository.GetTokenAsync(clientId, tokenId, CancellationToken.None))!;
        var firstTime = DateTimeOffset.UtcNow;
        var secondActor = Guid.NewGuid();

        // Both reads finish before the first write, without relying on sleeps or response timing.
        var firstWrite = firstRepository.UpdateTokenAsync(
            firstModel with { RevokedAt = firstTime, RevokedBy = actor }, CancellationToken.None);
        if (!parallel)
        {
            await firstWrite;
        }

        var secondWrite = secondRepository.UpdateTokenAsync(
            secondModel with { RevokedAt = firstTime.AddSeconds(1), RevokedBy = secondActor }, CancellationToken.None);
        var results = await Task.WhenAll(firstWrite, secondWrite);
        var original = results[0];
        var repeated = results[1];

        Assert.Equal(original.RevokedAt, repeated.RevokedAt);
        using var observer = _app.Services.CreateScope();
        var saved = await observer.ServiceProvider.GetRequiredService<AuthenticationDbContext>()
            .ClientTokens.AsNoTracking().SingleAsync(token => token.Id == tokenId);
        Assert.Equal(original.RevokedAt, saved.RevokedAt);
        Assert.Contains(saved.RevokedBy, new Guid?[] { actor, secondActor });
        if (!parallel)
        {
            Assert.Equal(actor, saved.RevokedBy);
        }

        Assert.Equal(new byte[] { 1, 2, 3 }, saved.SecretHash);
        Assert.Equal(["catalog_search"], saved.AllowedTools);
    }

    /// <summary>
    /// Leaves a different tracked entry's concurrency failure with the caller instead of reporting a Client conflict.
    /// </summary>
    /// <returns>The native entry identity and database atomicity assertions.</returns>
    [Fact]
    public async Task ClientUpdateDoesNotTranslateAnotherEntryConcurrencyFailure()
    {
        using var scope = _app!.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var owner = await provider.GetRequiredService<IOwnerRegistrationService>().RegisterAsync(
            new("owner", "owner@example.test", "Valid-test-Password!123"), CancellationToken.None);
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", owner.Id.ToString())], "fixture-owner"))
        };

        try
        {
            var service = provider.GetRequiredService<IClientService>();
            var issued = await service.CreateAsync(
                new ClientModel { ClientId = Guid.NewGuid(), DisplayName = "original", IsActive = true },
                new IssueClientTokenRequestDto(), CancellationToken.None);
            var dbContext = provider.GetRequiredService<AuthenticationDbContext>();
            dbContext.ChangeTracker.Clear();
            var model = (await service.GetAsync(issued.ClientId, CancellationToken.None))!;
            var missingToken = new AuthenticationClientTokenEntity
            {
                Id = Guid.NewGuid(), ClientId = issued.ClientId, RevokedAt = DateTimeOffset.UtcNow
            };
            dbContext.Attach(missingToken);
            dbContext.Entry(missingToken).Property(value => value.RevokedAt).IsModified = true;

            // One shared save can contain caller-staged entries unrelated to the Client being updated.
            var failure = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
                service.UpdateAsync(model with { DisplayName = "not-committed" }, CancellationToken.None));

            Assert.Same(missingToken, Assert.Single(failure.Entries).Entity);
            using var observer = _app.Services.CreateScope();
            Assert.Equal("original", (await observer.ServiceProvider.GetRequiredService<AuthenticationDbContext>()
                .Clients.AsNoTracking().SingleAsync(value => value.Id == issued.ClientId)).Name);
        }
        finally
        {
            accessor.HttpContext = null;
        }
    }

    /// <summary>
    /// Classifies the losing database write as a conflict when two requests both read the same current Client version.
    /// </summary>
    /// <returns>The concurrent write, persisted winner and audit preservation assertions.</returns>
    [Fact]
    public async Task ConcurrentClientUpdatesReturnOneBusinessConflict()
    {
        Guid clientId;
        Guid ownerId;
        using (var setup = _app!.Services.CreateScope())
        {
            var provider = setup.ServiceProvider;
            var owner = await provider.GetRequiredService<IOwnerRegistrationService>().RegisterAsync(
                new("owner", "owner@example.test", "Valid-test-Password!123"), CancellationToken.None);
            ownerId = owner.Id;
            provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", ownerId.ToString())], "fixture-owner"))
            };
            var issued = await provider.GetRequiredService<IClientService>().CreateAsync(
                new ClientModel { ClientId = Guid.NewGuid(), DisplayName = "original", IsActive = true },
                new IssueClientTokenRequestDto(), CancellationToken.None);
            clientId = issued.ClientId;
        }

        try
        {
            using var first = _app.Services.CreateScope();
            using var second = _app.Services.CreateScope();
            var firstService = first.ServiceProvider.GetRequiredService<IClientService>();
            var secondService = second.ServiceProvider.GetRequiredService<IClientService>();
            var firstModel = (await firstService.GetAsync(clientId, CancellationToken.None))!;
            var secondModel = (await secondService.GetAsync(clientId, CancellationToken.None))!;
            Assert.Equal(firstModel.ConcurrencyToken, secondModel.ConcurrencyToken);

            // Both requests have passed the original version check before either write begins.
            var results = await Task.WhenAll(
                Record.ExceptionAsync(() => firstService.UpdateAsync(firstModel with { DisplayName = "first" }, CancellationToken.None)),
                Record.ExceptionAsync(() => secondService.UpdateAsync(secondModel with { DisplayName = "second" }, CancellationToken.None)));
            Assert.Single(results, result => result is null);
            var conflict = Assert.IsType<BusinessException>(Assert.Single(results, result => result is not null));
            Assert.Equal(BusinessErrorKind.Conflict, conflict.Kind);

            using var observer = _app.Services.CreateScope();
            var stored = await observer.ServiceProvider.GetRequiredService<AuthenticationDbContext>()
                .Clients.AsNoTracking().SingleAsync(client => client.Id == clientId);
            Assert.Contains(stored.Name, new[] { "first", "second" });
            Assert.Equal(ownerId, stored.CreatedBy);
        }
        finally
        {
            _app.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = null;
        }
    }

    /// <summary>
    /// Preserves UTC invitation metadata and immutable creation timestamps after removing Authentication's injected clock.
    /// </summary>
    /// <returns>The persisted UTC and detached creation-field protection assertions.</returns>
    [Fact]
    public async Task NativeUtcKeepsInvitationLifetimeAndCreationFields()
    {
        var before = DateTimeOffset.UtcNow;
        Guid userId;
        DateTimeOffset createdAt;
        using (var scope = _app!.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            var service = provider.GetRequiredService<IUserService>();
            await scope.ServiceProvider.GetRequiredService<IOwnerRegistrationService>().RegisterAsync(
            new("owner", "owner@example.test", "Valid-test-Password!123"), CancellationToken.None);
            var first = await service.InviteAsync(new("invited", "invited@example.test", ["Owner"]), CancellationToken.None);
            var after = DateTimeOffset.UtcNow;
            Assert.InRange(first.InvitationExpiresAt, before.AddMinutes(30), after.AddMinutes(30));
            var users = provider.GetRequiredService<UserManager<User>>();
            var user = (await users.FindByIdAsync(first.User.Id.ToString()))!;
            userId = user.Id;

            // Compare PostgreSQL's persisted timestamp precision rather than sub-microsecond clock ticks.
            await provider.GetRequiredService<AuthenticationDbContext>().Entry(user).ReloadAsync();
            createdAt = user.CreatedAt;
            Assert.InRange(createdAt, before, after);
            Assert.Equal(TimeSpan.Zero, createdAt.Offset);

            user.CreatedAt = createdAt.AddYears(-1);
            user.Email = "updated@example.test";
            Assert.True((await users.UpdateAsync(user)).Succeeded);
        }

        // Verify persisted values from another context rather than trusting a modified tracked instance.
        using var verification = _app.Services.CreateScope();
        var stored = await verification.ServiceProvider.GetRequiredService<AuthenticationDbContext>()
            .Users.AsNoTracking().SingleAsync(user => user.Id == userId);
        Assert.Equal(createdAt, stored.CreatedAt);
        Assert.InRange(stored.UpdatedAt, createdAt, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Permits multiple Owners while keeping every password native and every normalized recovery email unique.
    /// </summary>
    /// <returns>The persisted memberships, password and case-normalized email assertions.</returns>
    [Fact]
    public async Task MultipleOwnersUseNativePasswordsAndUniqueRecoveryEmail()
    {
        using var scope = _app!.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IUserService>();
        await scope.ServiceProvider.GetRequiredService<IOwnerRegistrationService>().RegisterAsync(new("first-owner", "first@example.test", "Valid-test-Password!123"), CancellationToken.None);
        var second = await service.CreateAsync(new("second-owner", "second@example.test", "Other-native-Password!123"), CancellationToken.None);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = (await users.FindByIdAsync(second.Id.ToString()))!;

        Assert.True(await users.CheckPasswordAsync(user, "Other-native-Password!123"));
        Assert.True(await users.IsInRoleAsync(user, "Owner"));
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Users.CountAsync());
        var duplicate = await Assert.ThrowsAsync<BusinessException>(() => service.CreateAsync(
            new("third-owner", "SECOND@EXAMPLE.TEST", "Third-native-Password!123"), CancellationToken.None));
        Assert.Equal(BusinessErrorKind.Conflict, duplicate.Kind);
    }

    /// <summary>
    /// Prevents two independent registrations from committing accounts with the same normalized recovery address.
    /// </summary>
    /// <returns>The persisted uniqueness and single-commit assertions.</returns>
    [Fact]
    public async Task ConcurrentOwnerRegistrationsCannotShareRecoveryEmail()
    {
        using (var setup = _app!.Services.CreateScope())
        {
            await setup.ServiceProvider.GetRequiredService<IOwnerRegistrationService>().RegisterAsync(
                new("first-owner", "first@example.test", "Valid-test-Password!123"), CancellationToken.None);
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = new[] { AttemptAsync("owner-a"), AttemptAsync("owner-b") };
        start.SetResult();
        var failures = await Task.WhenAll(tasks);
        Assert.Single(failures, error => error is null);
        using var verification = _app.Services.CreateScope();
        Assert.Equal(1, await verification.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Users
            .CountAsync(user => user.NormalizedEmail == "SHARED@EXAMPLE.TEST"));

        // Every request owns its own context, and the database/native validator must enforce the race outcome.
        async Task<Exception?> AttemptAsync(string name)
        {
            using var scope = _app.Services.CreateScope();
            await start.Task;

            return await Record.ExceptionAsync(() => scope.ServiceProvider.GetRequiredService<IUserService>().CreateAsync(
                new(name, "shared@example.test", "Concurrent-native-Password!123"), CancellationToken.None));
        }
    }

    /// <summary>
    /// Releases only the fixture-owned provider and PostgreSQL container, including after a failing assertion.
    /// </summary>
    /// <returns>The asynchronous fixture cleanup.</returns>
    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        await _database.DisposeAsync();
    }

    /// <summary>
    /// Builds native cookie-validation input while preserving the fixture's real Identity store and managers.
    /// </summary>
    /// <param name="provider">The request scope containing the native cookie validator.</param>
    /// <param name="principal">The previously issued principal.</param>
    /// <param name="issuedAt">The test-controlled original ticket issue time.</param>
    /// <returns>The validation context observed after the native security-stamp check.</returns>
    private static CookieValidatePrincipalContext CreateCookieValidationContext(
        IServiceProvider provider, ClaimsPrincipal principal, DateTimeOffset issuedAt)
    {
        var http = new DefaultHttpContext { RequestServices = provider };
        http.Request.Scheme = "https";
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
        var scheme = new AuthenticationScheme(IdentityConstants.ApplicationScheme, null, typeof(CookieAuthenticationHandler));
        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(scheme.Name);
        var properties = new AuthenticationProperties { IssuedUtc = issuedAt, ExpiresUtc = issuedAt.AddHours(8) };

        return new CookieValidatePrincipalContext(http, scheme, options,
            new AuthenticationTicket(principal, properties, scheme.Name));
    }

    /// <summary>
    /// Injects only the known existence-probe reset or an explicitly requested DDL failure.
    /// </summary>
    private sealed class SchemaCreationInterceptor : DbCommandInterceptor
    {
        /// <summary>
        /// Gets whether schema initialization incorrectly asked the provider to check database existence.
        /// </summary>
        public bool ExistenceProbeObserved { get; private set; }

        /// <summary>
        /// Gets or sets the provider failure used only by the DDL propagation regression.
        /// </summary>
        public PostgresException? SchemaFailure { get; set; }

        /// <summary>
        /// Rejects an existence probe before it can be interpreted as an absent database.
        /// </summary>
        /// <param name="command">The generated schema or readiness command.</param>
        /// <param name="eventData">The native execution metadata.</param>
        /// <param name="result">The current interception decision.</param>
        /// <param name="cancellationToken">The operation cancellation token.</param>
        /// <returns>The unchanged decision for commands outside fault injection.</returns>
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Trim() == "SELECT 1")
            {
                ExistenceProbeObserved = true;

                // Keep the reset armed: table creation must avoid this probe entirely.
                throw new NpgsqlException("Injected database probe reset.",
                    new IOException("Injected connection reset.",
                        new SocketException((int)SocketError.ConnectionReset)));
            }

            if (SchemaFailure is { } failure && command.CommandText.Contains("CREATE TABLE", StringComparison.Ordinal))
            {
                throw failure;
            }

            return ValueTask.FromResult(result);
        }
    }

    /// <summary>
    /// Returns one controlled native validation result while retaining the real Identity stores and UoW.
    /// </summary>
    /// <param name="error">The native error whose classification is under test.</param>
    private sealed class RejectedRoleValidator(IdentityError error) : IRoleValidator<IdentityRole<Guid>>
    {
        /// <summary>
        /// Rejects role creation through the native result contract, before a role INSERT is attempted.
        /// </summary>
        /// <param name="manager">The native role manager.</param>
        /// <param name="role">The candidate role.</param>
        /// <returns>The controlled Identity failure.</returns>
        public Task<IdentityResult> ValidateAsync(RoleManager<IdentityRole<Guid>> manager, IdentityRole<Guid> role) =>
            Task.FromResult(IdentityResult.Failed(error));
    }

    /// <summary>
    /// Aligns two requests' absence checks without sleeps or production synchronization changes.
    /// </summary>
    private sealed class ProvisioningReadBarrier : DbCommandInterceptor
    {
        /// <summary>
        /// Counts only native normalized-role reads while this regression is enabled.
        /// </summary>
        private int _reads;

        /// <summary>
        /// Gates the initial existence checks and the native validators' subsequent checks separately.
        /// </summary>
        private readonly TaskCompletionSource[] _gates =
        [new(TaskCreationOptions.RunContinuationsAsynchronously), new(TaskCreationOptions.RunContinuationsAsynchronously)];

        /// <summary>
        /// Gets or sets whether the cold-registration scenario owns the barrier.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets an unrelated failure injected only when the native role INSERT reaches PostgreSQL.
        /// </summary>
        public Exception? RoleInsertFailure { get; set; }

        /// <summary>
        /// Preserves the chosen provider failure at the native persistence boundary.
        /// </summary>
        /// <param name="command">The generated native INSERT.</param>
        /// <param name="eventData">The execution metadata.</param>
        /// <param name="result">The existing interception decision.</param>
        /// <param name="cancellationToken">The operation cancellation token.</param>
        /// <returns>The unchanged decision for commands outside fault injection.</returns>
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (RoleInsertFailure is { } failure &&
                command.CommandText.StartsWith("INSERT INTO identity.\"Role\"", StringComparison.Ordinal))
            {
                throw failure;
            }

            return ValueTask.FromResult(result);
        }

        /// <summary>
        /// Holds the first two matching read pairs after PostgreSQL has produced their results.
        /// </summary>
        /// <param name="command">The native role query.</param>
        /// <param name="eventData">The command's execution metadata.</param>
        /// <param name="result">The reader still owned by EF.</param>
        /// <param name="cancellationToken">Cancels the bounded test rendezvous.</param>
        /// <returns>The unchanged native reader.</returns>
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.Contains("FROM identity.\"Role\"", StringComparison.Ordinal) &&
                command.CommandText.Contains("\"NormalizedName\"", StringComparison.Ordinal) &&
                command.CommandText.Contains("WHERE", StringComparison.Ordinal))
            {
                var ordinal = Interlocked.Increment(ref _reads);
                if (ordinal <= 4)
                {
                    var gate = _gates[(ordinal - 1) / 2];
                    if (ordinal % 2 == 0)
                    {
                        gate.TrySetResult();
                    }

                    await gate.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Targets native authorization saves or token bulk updates without changing their production implementation.
    /// </summary>
    private sealed class SessionCommandFailureInterceptor : DbCommandInterceptor
    {
        /// <summary>
        /// Gets or sets whether fault injection is enabled after arrangement.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets which native table's update fails.
        /// </summary>
        public bool AuthorizationFailure { get; set; }

        /// <summary>
        /// Gets or sets the caller cancellation source used only by the cancellation case.
        /// </summary>
        public CancellationTokenSource? Cancellation { get; set; }

        /// <summary>
        /// Gets the provider-boundary exception used to verify preservation of the primary failure.
        /// </summary>
        public InvalidOperationException Failure { get; } = new("Injected session command failure.");

        /// <summary>
        /// Gets or sets whether the next marker query pauses after its PostgreSQL snapshot is read.
        /// </summary>
        public bool PauseMarkerRead { get; set; }

        /// <summary>
        /// Signals that the grant transaction has read its old snapshot.
        /// </summary>
        public TaskCompletionSource MarkerRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Releases the grant only after logout has committed.
        /// </summary>
        public TaskCompletionSource ResumeGrant { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Holds only the first marker read so logout can commit in an independent transaction.
        /// </summary>
        /// <param name="command">The completed provider query.</param>
        /// <param name="eventData">The scoped query metadata.</param>
        /// <param name="result">The reader owning the transaction snapshot.</param>
        /// <param name="cancellationToken">Cancels a broken test synchronization.</param>
        /// <returns>The unchanged reader after the controlled overlap.</returns>
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (PauseMarkerRead && command.CommandText.StartsWith("SELECT EXISTS", StringComparison.Ordinal) &&
                command.CommandText.Contains("\"UserToken\"", StringComparison.Ordinal))
            {
                PauseMarkerRead = false;
                MarkerRead.TrySetResult();
                await ResumeGrant.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }

            return result;
        }

        /// <summary>
        /// Inspects a native bulk command before it executes.
        /// </summary>
        /// <param name="command">The native store command.</param>
        /// <param name="eventData">The execution metadata.</param>
        /// <param name="result">The current interception decision.</param>
        /// <param name="cancellationToken">The caller cancellation token.</param>
        /// <returns>The unchanged decision when this command is not targeted.</returns>
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            RejectTarget(command, cancellationToken);

            return ValueTask.FromResult(result);
        }

        /// <summary>
        /// Inspects Identity-model updates that return concurrency metadata through a reader.
        /// </summary>
        /// <param name="command">The native store update.</param>
        /// <param name="eventData">The execution metadata.</param>
        /// <param name="result">The current reader interception decision.</param>
        /// <param name="cancellationToken">The caller cancellation token.</param>
        /// <returns>The unchanged decision when this command is not targeted.</returns>
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            RejectTarget(command, cancellationToken);

            return ValueTask.FromResult(result);
        }

        /// <summary>
        /// Injects one narrowly selected provider failure after the request has reached native persistence.
        /// </summary>
        /// <param name="command">The generated update command.</param>
        /// <param name="cancellationToken">The token whose cancellation must remain observable.</param>
        private void RejectTarget(DbCommand command, CancellationToken cancellationToken)
        {
            var table = AuthorizationFailure ? "Authorization" : "Token";
            if (!Enabled || !command.CommandText.StartsWith("UPDATE openiddict.\"" + table + "\"", StringComparison.Ordinal))
            {
                return;
            }

            if (Cancellation is not null)
            {
                Cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }

            throw Failure;
        }
    }

}
