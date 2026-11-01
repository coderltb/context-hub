namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Verifies real Identity/OpenIddict transactions and Redis budgets in disposable containers, never a product database.
/// </summary>
[Collection(nameof(GlobalMappingCollection))]
public sealed class PasswordRecoveryPersistenceTests : IAsyncLifetime
{
    /// <summary>
    /// Owns isolated native Identity and OpenIddict data for one test instance.
    /// </summary>
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("password_recovery_tests").WithUsername("postgres").WithPassword("postgres").Build();

    /// <summary>
    /// Owns real atomic coordination without substituting an in-memory authority store.
    /// </summary>
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    /// <summary>
    /// Replaces only external email delivery so native generated proof can be verified without sending live mail.
    /// </summary>
    private readonly CapturingEmailService _email = new();

    /// <summary>
    /// Injects a targeted database-save failure only for the rollback test.
    /// </summary>
    private readonly RevocationFailureInterceptor _failure = new();

    /// <summary>
    /// Owns the production service registration and native managers.
    /// </summary>
    private WebApplication? _app;

    /// <summary>
    /// Creates mapped tables only inside the fresh test database and registers the real Redis endpoint.
    /// </summary>
    /// <returns>The disposable native test environment.</returns>
    public async Task InitializeAsync()
    {
        await Task.WhenAll(_database.StartAsync(), _redis.StartAsync());
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
            ["CacheSettings:RedisSettings:Host"] = _redis.Hostname,
            ["CacheSettings:RedisSettings:Port"] = _redis.GetMappedPublicPort(6379).ToString(),
            ["CacheSettings:RedisSettings:SyncTimeout"] = "1000",
            ["CacheSettings:RedisSettings:AsyncTimeout"] = "1000",
            ["Authentication:ClientTokenPepper"] = Convert.ToBase64String(new byte[32]),
            ["Authentication:IntrospectionClientSecret"] = new string('x', 32)
        });
        builder.Services.AddLoggingInfrastructure(builder.Configuration, "recovery-persistence-tests", Environments.Development);
        builder.Services.AddAuthenticationApi(builder.Configuration, builder.Environment);
        builder.Services.AddPostgreSqlFixture<AuthenticationDbContext>(_database.GetConnectionString());
        builder.Services.RemoveAll<IEmailService>();
        builder.Services.AddSingleton<IEmailService>(_email);
        builder.Services.AddDbContext<AuthenticationDbContext>((_, options) => options.AddInterceptors(_failure));
        _app = builder.Build();
        using var scope = _app.Services.CreateScope();

        // Testcontainers already created the database; create only its mapped tables.
        await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>()
            .GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        await scope.ServiceProvider.GetRequiredService<IOwnerRegistrationService>().RegisterAsync(
            new("owner", "owner@example.test", "Original-native-Password!123"), CancellationToken.None);
    }

    /// <summary>
    /// Resets the password and revokes human grants together while preserving native account settings and independently issued client credentials.
    /// </summary>
    /// <returns>The native proof, persisted password and ownership assertions.</returns>
    [Fact]
    public async Task NativeResetCommitsAndRevokesOnlyHumanGrants()
    {
        Guid ownerId;
        string oldStamp;
        string code;
        Guid clientTokenId;
        using (var scope = _app!.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            var users = provider.GetRequiredService<UserManager<User>>();
            var user = (await users.FindByNameAsync("owner"))!;
            ownerId = user.Id;
            Assert.True((await users.SetTwoFactorEnabledAsync(user, true)).Succeeded);
            oldStamp = user.SecurityStamp!;
            await CreateHumanGrantAsync(provider, user);
            var db = provider.GetRequiredService<AuthenticationDbContext>();
            var client = new AuthenticationClientEntity { Name = "independent-client", CreatedBy = ownerId, UpdatedBy = ownerId };
            var clientToken = new AuthenticationClientTokenEntity
            {
                Id = Guid.NewGuid(), Client = client, SecretHash = new byte[32], Prefix = "test",
                CreatedBy = ownerId, AllowedTools = ["catalog_search"]
            };

            // Seed audited client records as an authenticated Owner; recovery itself still has no client-write capability.
            var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
            httpContextAccessor.HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("sub", ownerId.ToString())], "fixture-owner"))
            };
            try
            {
                db.ClientTokens.Add(clientToken);
                await db.SaveChangesAsync();
            }
            finally
            {
                httpContextAccessor.HttpContext = null;
            }

            clientTokenId = clientToken.Id;
            var recovery = provider.GetRequiredService<IPasswordRecoveryService>();
            var account = await recovery.GetAccountAsync("OWNER@EXAMPLE.TEST", CancellationToken.None);
            Assert.NotNull(account);
            Assert.True(await recovery.SendOtpAsync(account, CancellationToken.None));
            code = GetCapturedCode();
            Assert.Matches("^[0-9]{6}$", code);
            Assert.NotNull(await recovery.VerifyAsync(new() { Email = account.Email, OtpCode = code }, CancellationToken.None));

            // Resend does not promise a new native code, and Verify alone must not consume password-reset authority.
            Assert.True(await recovery.SendOtpAsync(account, CancellationToken.None));
            Assert.True(await users.VerifyUserTokenAsync(user, users.Options.Tokens.PasswordResetTokenProvider,
                UserManager<User>.ResetPasswordTokenPurpose, code));
            Assert.Equal(PasswordResetOutcome.Succeeded, await recovery.ResetAsync(
                new(ownerId, code, "Replacement-native-Password!123"), CancellationToken.None));
        }

        using var verification = _app.Services.CreateScope();
        var verifiedUsers = verification.ServiceProvider.GetRequiredService<UserManager<User>>();
        var stored = (await verifiedUsers.FindByIdAsync(ownerId.ToString()))!;
        Assert.True(await verifiedUsers.CheckPasswordAsync(stored, "Replacement-native-Password!123"));
        Assert.False(await verifiedUsers.CheckPasswordAsync(stored, "Original-native-Password!123"));
        Assert.NotEqual(oldStamp, stored.SecurityStamp);
        Assert.True(stored.TwoFactorEnabled);
        Assert.False(await verifiedUsers.VerifyUserTokenAsync(stored, verifiedUsers.Options.Tokens.PasswordResetTokenProvider,
            UserManager<User>.ResetPasswordTokenPurpose, code));
        var dbAfter = verification.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        Assert.Equal("revoked", Assert.Single(await dbAfter.Set<NativeAuthenticationToken>().ToListAsync()).Status);
        Assert.Null((await dbAfter.ClientTokens.SingleAsync(token => token.Id == clientTokenId)).RevokedAt);
    }

    /// <summary>
    /// Rolls back the already saved native password when revoking a human token later fails inside the owned transaction.
    /// </summary>
    /// <returns>The original failure identity and independently read database rollback assertions.</returns>
    [Fact]
    public async Task GrantRevocationFailureRollsBackPasswordAndSecurityStamp()
    {
        Guid ownerId;
        string oldStamp;
        using (var scope = _app!.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            var users = provider.GetRequiredService<UserManager<User>>();
            var user = (await users.FindByNameAsync("owner"))!;
            ownerId = user.Id;
            oldStamp = user.SecurityStamp!;
            await CreateHumanGrantAsync(provider, user);
            var code = await users.GeneratePasswordResetTokenAsync(user);
            _failure.Enabled = true;
            var recovery = provider.GetRequiredService<IPasswordRecoveryService>();

            Assert.Same(_failure.Failure, await Assert.ThrowsAsync<InvalidOperationException>(() => recovery.ResetAsync(
                new(ownerId, code, "Must-rollback-Password!123"), CancellationToken.None)));
        }

        using var verification = _app.Services.CreateScope();
        var usersAfter = verification.ServiceProvider.GetRequiredService<UserManager<User>>();
        var stored = (await usersAfter.FindByIdAsync(ownerId.ToString()))!;
        Assert.Equal(oldStamp, stored.SecurityStamp);
        Assert.True(await usersAfter.CheckPasswordAsync(stored, "Original-native-Password!123"));
        Assert.False(await usersAfter.CheckPasswordAsync(stored, "Must-rollback-Password!123"));
        var db = verification.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        Assert.Equal("valid", Assert.Single(await db.Set<NativeAuthenticationToken>().ToListAsync()).Status);
    }

    /// <summary>
    /// Makes current-password changes atomic with native human-grant revocation, including a failed revocation save.
    /// </summary>
    /// <param name="failRevocation">Whether the existing targeted interceptor rejects the token update.</param>
    /// <returns>The independent-context credential, stamp and token-status assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CurrentPasswordChangeSharesTheGrantTransaction(bool failRevocation)
    {
        Guid id;
        string? originalStamp;
        using (var scope = _app!.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            var userManager = provider.GetRequiredService<UserManager<User>>();
            var user = (await userManager.FindByNameAsync("owner"))!;
            id = user.Id;
            originalStamp = user.SecurityStamp;
            await CreateHumanGrantAsync(provider, user);
            _failure.Enabled = failRevocation;
            var userService = provider.GetRequiredService<IUserService>();
            var request = new ChangePasswordRequestDto("Original-native-Password!123", "Current-change-Password!456");

            if (failRevocation)
            {
                Assert.Same(_failure.Failure, await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    userService.ChangePasswordAsync(id, request, CancellationToken.None)));
            }
            else
            {
                await userService.ChangePasswordAsync(id, request, CancellationToken.None);
            }
        }

        // Observe the commit or rollback from a fresh scoped context, never the operation's tracked entity.
        using var observer = _app.Services.CreateScope();
        var manager = observer.ServiceProvider.GetRequiredService<UserManager<User>>();
        var saved = (await manager.FindByIdAsync(id.ToString()))!;
        Assert.Equal(!failRevocation, await manager.CheckPasswordAsync(saved, "Current-change-Password!456"));
        Assert.Equal(failRevocation, await manager.CheckPasswordAsync(saved, "Original-native-Password!123"));
        Assert.Equal(failRevocation, originalStamp == saved.SecurityStamp);
        var db = observer.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        Assert.Equal(failRevocation ? "valid" : "revoked", Assert.Single(await db.Set<NativeAuthenticationToken>().ToListAsync()).Status);
    }

    /// <summary>
    /// Preserves native password policy and rejects proof from a different token purpose without changing account state.
    /// </summary>
    /// <returns>The native rejection and proof-reuse assertions.</returns>
    [Fact]
    public async Task PasswordPolicyAndInvitationPurposeRemainNative()
    {
        using var scope = _app!.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var users = provider.GetRequiredService<UserManager<User>>();
        var user = (await users.FindByNameAsync("owner"))!;
        var code = await users.GeneratePasswordResetTokenAsync(user);
        var recovery = provider.GetRequiredService<IPasswordRecoveryService>();
        Assert.Equal(PasswordResetOutcome.PasswordRejected, await recovery.ResetAsync(new(user.Id, code, "weak"), CancellationToken.None));
        Assert.True(await users.VerifyUserTokenAsync(user, users.Options.Tokens.PasswordResetTokenProvider,
            UserManager<User>.ResetPasswordTokenPurpose, code));
        Assert.False(await users.VerifyUserTokenAsync(user, AuthenticationConstants.INVITATION_PROVIDER,
            AuthenticationConstants.INVITATION_PURPOSE, code));
    }

    /// <summary>
    /// Allows only one competing password reset to persist when both requests begin with the same native proof.
    /// </summary>
    /// <returns>The independent-scope concurrency and stored-password assertions.</returns>
    [Fact]
    public async Task ConcurrentResetCannotRedeemTheSameNativeProofTwice()
    {
        Guid ownerId;
        string code;
        using (var scope = _app!.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = (await users.FindByNameAsync("owner"))!;
            ownerId = user.Id;
            code = await users.GeneratePasswordResetTokenAsync(user);
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = new[] { AttemptAsync("First-reset-Password!123"), AttemptAsync("Other-reset-Password!123") };
        start.SetResult();
        var outcomes = await Task.WhenAll(tasks);
        Assert.Single(outcomes, outcome => outcome == PasswordResetOutcome.Succeeded);
        Assert.Single(outcomes, outcome => outcome == PasswordResetOutcome.Rejected);
        using var verification = _app.Services.CreateScope();
        var usersAfter = verification.ServiceProvider.GetRequiredService<UserManager<User>>();
        var stored = (await usersAfter.FindByIdAsync(ownerId.ToString()))!;
        Assert.False(await usersAfter.CheckPasswordAsync(stored, "Original-native-Password!123"));
        Assert.True(await usersAfter.CheckPasswordAsync(stored, "First-reset-Password!123") ||
            await usersAfter.CheckPasswordAsync(stored, "Other-reset-Password!123"));

        // Native EF concurrency and security-stamp validation decide the winner; no application retry or lock engine is added.
        async Task<PasswordResetOutcome> AttemptAsync(string password)
        {
            using var scope = _app.Services.CreateScope();
            await start.Task;

            return await scope.ServiceProvider.GetRequiredService<IPasswordRecoveryService>()
                .ResetAsync(new(ownerId, code, password), CancellationToken.None);
        }
    }

    /// <summary>
    /// Shares attempts across scopes and prevents resend from replenishing an exhausted native verification budget.
    /// </summary>
    /// <returns>The real Redis budget, expiry and native-proof rejection assertions.</returns>
    [Fact]
    public async Task RedisBudgetsSurviveResendAndCrossBrowserRequests()
    {
        string code;
        using (var scope = _app!.Services.CreateScope())
        {
            var recovery = scope.ServiceProvider.GetRequiredService<IPasswordRecoveryService>();
            Assert.True(await recovery.ReserveRequestAsync(new() { Email = "owner@example.test" }, CancellationToken.None));
            Assert.False(await recovery.ReserveRequestAsync(new() { Email = "OWNER@EXAMPLE.TEST" }, CancellationToken.None));
            Assert.True(await recovery.SendOtpAsync((await recovery.GetAccountAsync("owner@example.test", CancellationToken.None))!, CancellationToken.None));
            code = GetCapturedCode();
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var browser = _app.Services.CreateScope();
            Assert.Null(await browser.ServiceProvider.GetRequiredService<IPasswordRecoveryService>().VerifyAsync(
                new() { Email = "owner@example.test", OtpCode = "not-a-numeric-code" }, CancellationToken.None));
        }

        using var final = _app.Services.CreateScope();
        var recoveryAfter = final.ServiceProvider.GetRequiredService<IPasswordRecoveryService>();
        await recoveryAfter.SendOtpAsync((await recoveryAfter.GetAccountAsync("owner@example.test", CancellationToken.None))!, CancellationToken.None);
        Assert.Null(await recoveryAfter.VerifyAsync(new() { Email = "owner@example.test", OtpCode = code }, CancellationToken.None));
        var key = CacheKey.Create(AuthenticationRecoveryConstants.CACHE_NAMESPACE, 1, "verify", "OWNER@EXAMPLE.TEST");
        var redis = _app.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        Assert.InRange((await redis.KeyTimeToLiveAsync(key.Value))!.Value, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
        Assert.DoesNotContain("example.test", key.Value);
    }

    /// <summary>
    /// Keeps the hourly limit fixed across resend reservations without extending the counter lifetime.
    /// </summary>
    /// <returns>The actual Redis cooldown, hourly cap and non-renewing TTL assertions.</returns>
    [Fact]
    public async Task SendBudgetHasFixedExpiryAndUnknownAccountsUseTheSameLimit()
    {
        using var scope = _app!.Services.CreateScope();
        var recovery = scope.ServiceProvider.GetRequiredService<IPasswordRecoveryService>();
        var redis = _app.Services.GetRequiredService<IConnectionMultiplexer>().GetDatabase();
        var normalized = scope.ServiceProvider.GetRequiredService<UserManager<User>>().NormalizeEmail("unknown@example.test");
        var cooldown = CacheKey.Create(AuthenticationRecoveryConstants.CACHE_NAMESPACE, 1,
            AuthenticationRecoveryConstants.COOLDOWN_KEY, normalized);
        var hourly = CacheKey.Create(AuthenticationRecoveryConstants.CACHE_NAMESPACE, 1,
            AuthenticationRecoveryConstants.SEND_BUDGET_KEY, normalized);
        Assert.True(await recovery.ReserveRequestAsync(new() { Email = "unknown@example.test" }, CancellationToken.None));
        var initialTtl = (await redis.KeyTimeToLiveAsync(hourly.Value))!.Value;
        Assert.Null(await recovery.GetAccountAsync("unknown@example.test", CancellationToken.None));

        for (var request = 1; request < 5; request++)
        {
            // Expire only the test-owned cooldown explicitly; no sleep or production clock override creates the scenario.
            await redis.KeyDeleteAsync(cooldown.Value);
            Assert.True(await recovery.ReserveRequestAsync(new() { Email = "unknown@example.test" }, CancellationToken.None));
        }

        await redis.KeyDeleteAsync(cooldown.Value);
        Assert.False(await recovery.ReserveRequestAsync(new() { Email = "unknown@example.test" }, CancellationToken.None));
        Assert.True((await redis.KeyTimeToLiveAsync(hourly.Value))!.Value <= initialTtl);
        Assert.Empty(_email.Messages);
    }

    /// <summary>
    /// Preserves caller cancellation and rejects recovery during a real Redis outage without a memory fallback.
    /// </summary>
    /// <returns>The native cancellation and provider-failure assertions.</returns>
    [Fact]
    public async Task RedisOutageAndCallerCancellationDoNotGrantRecovery()
    {
        using var scope = _app!.Services.CreateScope();
        var recovery = scope.ServiceProvider.GetRequiredService<IPasswordRecoveryService>();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => recovery.ReserveRequestAsync(
            new() { Email = "owner@example.test" }, cancellation.Token));

        await _redis.StopAsync();
        var failure = await Record.ExceptionAsync(() => recovery.ReserveRequestAsync(
            new() { Email = "unknown@example.test" }, CancellationToken.None));
        Assert.True(failure is RedisException or RedisTimeoutException);
        Assert.Empty(_email.Messages);
    }

    /// <summary>
    /// Releases the host and only the disposable test containers.
    /// </summary>
    /// <returns>The fixture cleanup operation.</returns>
    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        await _redis.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>
    /// Gets proof from the captured native message without persisting it or printing it in diagnostics.
    /// </summary>
    /// <returns>The generated native six-digit code.</returns>
    private string GetCapturedCode() => Regex.Match(_email.Messages[^1].PlainText, "\\b[0-9]{6}\\b").Value;

    /// <summary>
    /// Creates native human authorization/token rows solely as arrangement for revocation persistence tests.
    /// </summary>
    /// <param name="provider">The test's scoped native managers.</param>
    /// <param name="user">The arranged Owner account.</param>
    /// <returns>The persisted native token arrangement.</returns>
    private static async Task CreateHumanGrantAsync(IServiceProvider provider, User user)
    {
        var principal = await provider.GetRequiredService<ISessionService>().CreatePrincipalAsync(
            new(user.Id, user.UserName), new("test-session", "context-hub-admin", ["openid", "contexthub.admin"]), CancellationToken.None);
        Assert.NotNull(principal);
        await provider.GetRequiredService<IOpenIddictTokenManager>().CreateAsync(new OpenIddictTokenDescriptor
        {
            AuthorizationId = principal.GetAuthorizationId(), Subject = user.Id.ToString(),
            Type = OpenIddictConstants.TokenTypeHints.RefreshToken, Status = OpenIddictConstants.Statuses.Valid,
            CreationDate = DateTimeOffset.UtcNow, ExpirationDate = DateTimeOffset.UtcNow.AddHours(1)
        });
    }

    /// <summary>
    /// Captures only test email delivery while allowing native Identity to generate the actual code.
    /// </summary>
    private sealed class CapturingEmailService : IEmailService
    {
        /// <summary>
        /// Gets the test-only messages without writing them to a logger.
        /// </summary>
        public List<EmailRequestDto> Messages { get; } = [];

        /// <summary>
        /// Records the actual native proof message at the external-provider boundary.
        /// </summary>
        /// <param name="request">The native-generated message.</param>
        /// <param name="cancellationToken">The caller cancellation token.</param>
        /// <returns>Scripted provider acceptance, never a claim of real inbox delivery.</returns>
        public Task<bool> SendAsync(EmailRequestDto request, CancellationToken cancellationToken)
        {
            Messages.Add(request);

            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Fails the native bulk token update after the password write, proving rollback at the database-command boundary.
    /// </summary>
    private sealed class RevocationFailureInterceptor : DbCommandInterceptor
    {
        /// <summary>
        /// Gets or sets whether this test should reject the targeted command.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets the primary failure whose identity must survive transaction rollback and cleanup.
        /// </summary>
        public InvalidOperationException Failure { get; } = new("Injected test revocation command failure.");

        /// <summary>
        /// Rejects the native token bulk command while allowing unrelated account and authorization writes.
        /// </summary>
        /// <param name="command">The provider command generated by the native store.</param>
        /// <param name="eventData">The command execution metadata.</param>
        /// <param name="result">The unchanged interception decision.</param>
        /// <param name="cancellationToken">The operation cancellation token.</param>
        /// <returns>The native interception decision unless the command fault is enabled.</returns>
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.StartsWith("UPDATE openiddict.\"Token\"", StringComparison.Ordinal))
            {
                throw Failure;
            }

            return ValueTask.FromResult(result);
        }
    }
}
