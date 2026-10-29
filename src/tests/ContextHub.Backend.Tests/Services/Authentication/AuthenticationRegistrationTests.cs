namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Checks native options binding and DB-first registration without connecting to a database or issuing credentials.
/// </summary>
[Collection(nameof(GlobalMappingCollection))]
public sealed class AuthenticationRegistrationTests
{
    /// <summary>
    /// Rejects malformed role elements while preserving each command's collection contract.
    /// </summary>
    /// <returns>The request-validation assertions without a database connection.</returns>
    [Fact]
    public async Task RoleValidationRejectsMalformedElementsAndKeepsEmptyReplacement()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        using var provider = services.BuildServiceProvider();
        var invitationValidator = provider.GetRequiredService<IValidator<InviteUserCommand>>();
        var replacementValidator = provider.GetRequiredService<IValidator<ReplaceUserRolesCommand>>();

        foreach (var role in new string?[] { null, "", " ", "\t" })
        {
            // A valid role must not hide an invalid element later in the submitted collection.
            var invitation = new InviteUserCommand(new("invited", "invited@example.test", ["Owner", role!]));
            var replacement = new ReplaceUserRolesCommand(Guid.NewGuid(), new(["Owner", role!], "version"));
            Assert.False((await invitationValidator.ValidateAsync(invitation)).IsValid);
            Assert.False((await replacementValidator.ValidateAsync(replacement)).IsValid);
        }

        Assert.True((await invitationValidator.ValidateAsync(
            new(new("invited", "invited@example.test", ["Owner"])))).IsValid);
        Assert.False((await invitationValidator.ValidateAsync(
            new(new("invited", "invited@example.test", [])))).IsValid);
        Assert.True((await replacementValidator.ValidateAsync(
            new(Guid.NewGuid(), new([], "version")))).IsValid);
        Assert.False((await replacementValidator.ValidateAsync(
            new(Guid.NewGuid(), new(null!, "version")))).IsValid);
    }

    /// <summary>
    /// Converts failure to obtain the shared Redis connection into native unhealthy evidence without affecting liveness.
    /// </summary>
    /// <returns>The safe native dependency failure report.</returns>
    [Fact]
    public async Task RedisConnectionFactoryFailureBecomesUnhealthy()
    {
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        services.AddSingleton<IConnectionMultiplexer>(_ => throw new RedisConnectionException(
            ConnectionFailureType.UnableToConnect, "private-connection-details"));
        services.AddHealthInfrastructure();
        services.AddAuthenticationInfrastructure(CreateConfiguration(), new TestEnvironment());
        using var provider = services.BuildServiceProvider();
        var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync(
            check => check.Tags.Contains("recovery"));

        Assert.Equal(HealthStatus.Unhealthy, report.Status);
        Assert.Single(report.Entries);
    }

    /// <summary>
    /// Separates required database readiness from recovery coordination without resolving either during liveness.
    /// </summary>
    /// <returns>The native registration and isolated liveness assertions.</returns>
    [Fact]
    public async Task DependencyHealthUsesSeparateTagsAndLeavesLivenessIndependent()
    {
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        services.AddHealthInfrastructure();
        services.AddAuthenticationInfrastructure(CreateConfiguration(), new TestEnvironment());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var checks = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        Assert.Equal(3, checks.Count);
        Assert.Equal(["ready"], Assert.Single(checks, check => check.Name == "postgresql").Tags);
        Assert.Equal(["recovery"], Assert.Single(checks, check => check.Name == "redis").Tags);
        var report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync(
            registration => registration.Tags.Contains("live"));
        Assert.Equal(HealthStatus.Healthy, report.Status);
        Assert.Single(report.Entries);
    }

    /// <summary>
    /// Keeps the native health registration process-only and separate from database or Redis readiness.
    /// </summary>
    [Fact]
    public void HealthConfigurationRegistersOneNativeLivenessCheck()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddHealthInfrastructure();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        var self = Assert.Single(options.Registrations);

        Assert.Equal(HealthConstants.SELF, self.Name);
        Assert.Contains(HealthConstants.LIVENESS_TAG, self.Tags);
        Assert.DoesNotContain(HealthConstants.READINESS_TAG, self.Tags);
    }

    /// <summary>
    /// Keeps one native cleanup job, its pruning policy and shutdown behavior without starting background database work.
    /// </summary>
    /// <returns>The completed registration check after the unstarted scheduler has been released.</returns>
    [Fact]
    public async Task QuartzUsesOneNativeCleanupJobAndItsDefaultRetention()
    {
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        services.AddAuthenticationInfrastructure(CreateConfiguration(), new TestEnvironment());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var schedulerFactory = provider.GetRequiredService<ISchedulerFactory>();
        var scheduler = await schedulerFactory.GetScheduler();

        try
        {
            // Inspect the real in-memory schedule without starting its trigger or touching an Identity database.
            Assert.Equal(SchedulerStatus.Created, scheduler.Status);
            var job = await scheduler.GetJobDetail(OpenIddictQuartzJob.Identity);
            Assert.NotNull(job);
            Assert.Equal(typeof(OpenIddictQuartzJob), job.JobType);
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ISchedulerFactory));
            Assert.Single(await scheduler.GetTriggersOfJob(job.Key));

            var pruning = provider.GetRequiredService<IOptions<OpenIddictQuartzOptions>>().Value;
            Assert.False(pruning.DisableAuthorizationPruning);
            Assert.False(pruning.DisableTokenPruning);
            Assert.Equal(TimeSpan.FromDays(14), pruning.MinimumAuthorizationLifespan);
            Assert.Equal(TimeSpan.FromDays(14), pruning.MinimumTokenLifespan);
            Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(QuartzHostedService));
            Assert.True(provider.GetRequiredService<IOptions<QuartzHostedServiceOptions>>().Value.WaitForJobsToComplete);
        }
        finally
        {
            // The test owns this unstarted scheduler; release native resources even when an assertion fails.
            await scheduler.Shutdown();
        }
    }

    /// <summary>
    /// Copies only approved provisioning metadata while keeping generated IDs, credentials and audit state server-owned.
    /// </summary>
    [Fact]
    public void InvitationMappingOnlyCopiesApprovedMetadata()
    {
        new ServiceCollection().AddMapping();
        var request = new InviteUserRequestDto("  owner  ", "owner@example.test", ["Owner"], false);

        var user = request.Adapt<User>();

        Assert.Equal("owner", user.UserName);
        Assert.Equal(request.Email, user.Email);
        Assert.False(user.IsActive);
        Assert.Equal(Guid.Empty, user.Id);
        Assert.Null(user.PasswordHash);
        Assert.False(user.LockoutEnabled);
        Assert.False(user.TwoFactorEnabled);
        Assert.Equal(default, user.CreatedAt);
    }

    /// <summary>
    /// Maps only approved account metadata and keeps submitted password material out of the Identity entity.
    /// </summary>
    [Fact]
    public void DirectOwnerMappingDoesNotPopulateCredentialState()
    {
        new ServiceCollection().AddMapping();
        var request = new CreateUserRequestDto("  owner  ", "  owner@example.test  ", "never-log-password");
        var user = request.Adapt<User>();

        Assert.Equal("owner", user.UserName);
        Assert.Equal("owner@example.test", user.Email);
        Assert.True(user.IsActive);
        Assert.Null(user.PasswordHash);
        Assert.Null(user.SecurityStamp);
        Assert.Equal(Guid.Empty, user.Id);
        Assert.DoesNotContain("never-log-password", request.ToString()!);
    }

    /// <summary>
    /// Resolves connection settings through DI instead of capturing a second settings object during registration.
    /// </summary>
    [Fact]
    public void RegistrationUsesBoundOptionsForTheContext()
    {
        var configuration = CreateConfiguration();
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        services.AddAuthenticationInfrastructure(configuration, new TestEnvironment());

        // The provider callback must read the same configured options that other consumers receive.
        configuration["ConnectionStrings:Authentication"] = "Host=localhost;Database=bound_options;Username=test";
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();

        Assert.Equal(configuration.GetConnectionString("Authentication"), db.Database.GetDbConnection().ConnectionString);
        Assert.Equal(ConnectionState.Closed, db.Database.GetDbConnection().State);
    }

    /// <summary>
    /// Keeps deployment callback allowlists exact instead of appending development endpoints from options initializers.
    /// </summary>
    [Fact]
    public void AuthenticationOptionsUseOnlyConfiguredDeploymentEndpoints()
    {
        var configuration = CreateConfiguration();
        configuration["Authentication:Issuer"] = "https://identity.example.test/";
        configuration["Authentication:RedirectUris:0"] = "https://admin.example.test/auth/callback";
        configuration["Authentication:PostLogoutRedirectUris:0"] = "https://admin.example.test/signed-out";
        configuration["Authentication:CorsOrigins:0"] = "https://admin.example.test";
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        services.AddAuthenticationInfrastructure(configuration, new TestEnvironment());
        using var provider = services.BuildServiceProvider();

        // Use the real options binder; matching production values must not inherit a second localhost allowlist entry.
        var options = provider.GetRequiredService<IOptions<AuthenticationServiceOptions>>().Value;
        Assert.Equal("https://identity.example.test/", options.Issuer);
        Assert.Equal(["https://admin.example.test/auth/callback"], options.RedirectUris);
        Assert.Equal(["https://admin.example.test/signed-out"], options.PostLogoutRedirectUris);
        Assert.Equal(["https://admin.example.test"], options.CorsOrigins);
    }

    /// <summary>
    /// Leaves missing endpoint settings empty so committed Development values are not implicit deployment policy.
    /// </summary>
    [Fact]
    public void AuthenticationOptionsDoNotSupplyDevelopmentEndpointDefaults()
    {
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        services.AddAuthenticationInfrastructure(CreateConfiguration(), new TestEnvironment());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthenticationServiceOptions>>().Value;

        Assert.Empty(options.Issuer);
        Assert.Empty(options.RedirectUris);
        Assert.Empty(options.PostLogoutRedirectUris);
        Assert.Empty(options.CorsOrigins);
    }

    /// <summary>
    /// Leaves migrations history unconfigured because the service uses a provisioned DB-first schema.
    /// </summary>
    [Fact]
    public void RegistrationDoesNotConfigureMigrationHistory()
    {
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        services.AddAuthenticationInfrastructure(CreateConfiguration(), new TestEnvironment());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<AuthenticationDbContext>>();
        var relational = Assert.Single(options.Extensions.OfType<RelationalOptionsExtension>());

        Assert.Null(relational.MigrationsHistoryTableName);
        Assert.Null(relational.MigrationsHistoryTableSchema);
    }

    /// <summary>
    /// Retains the native secure cookie contract while moving its configuration out of the composition root.
    /// </summary>
    [Fact]
    public void CookieKeepsItsSecurityAndSessionSettings()
    {
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        services.AddAuthenticationInfrastructure(CreateConfiguration(), new TestEnvironment());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);

        Assert.Equal("__Host-context-hub-sso", options.Cookie.Name);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, options.Cookie.SameSite);
        Assert.Equal("/login", options.LoginPath.Value);
        Assert.Equal("/access-denied", options.AccessDeniedPath.Value);
        Assert.Equal(TimeSpan.FromHours(8), options.ExpireTimeSpan);
        Assert.False(options.SlidingExpiration);
    }

    /// <summary>
    /// Uses unique email and the existing numeric email provider for password recovery rather than a custom OTP engine.
    /// </summary>
    [Fact]
    public void IdentityUsesUniqueEmailAndNativeEmailPasswordReset()
    {
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        services.AddAuthenticationInfrastructure(CreateConfiguration(), new TestEnvironment());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.True(options.User.RequireUniqueEmail);
        Assert.Equal(TokenOptions.DefaultEmailProvider, options.Tokens.PasswordResetTokenProvider);
        Assert.Contains(TokenOptions.DefaultEmailProvider, options.Tokens.ProviderMap.Keys);
    }

    /// <summary>
    /// Restricts password-reset proof to its own short cookie rather than the Owner authentication scheme.
    /// </summary>
    [Fact]
    public void RecoveryCookieIsShortLivedAndDoesNotRedirectToOwnerLogin()
    {
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        services.AddAuthenticationInfrastructure(CreateConfiguration(), new TestEnvironment());
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get("Authentication.PasswordRecovery");

        Assert.Equal("__Host-context-hub-recovery", options.Cookie.Name);
        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal(TimeSpan.FromMinutes(5), options.ExpireTimeSpan);
        Assert.False(options.SlidingExpiration);
    }

    /// <summary>
    /// Rejects unknown or ambiguous legacy email before selecting an arbitrary account or invoking credential managers.
    /// </summary>
    /// <param name="matches">The scripted count across active and inactive legacy accounts.</param>
    /// <returns>The fail-closed cardinality and no-entity-read assertions.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task RecoveryRequiresExactlyOneAccountForTheNormalizedEmail(long matches)
    {
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        var configuration = CreateConfiguration();
        configuration["EmailSettings:ApiKey"] = "unit-test-only";
        configuration["ConnectionStrings:Authentication"] = "Host=127.0.0.1;Port=1;Database=unit_only;Username=test;Timeout=1";
        services.AddAuthenticationInfrastructure(configuration, new TestEnvironment());
        services.AddSingleton(new EmailCardinality(matches));
        services.AddScoped<IUserRepository, AmbiguousEmailRepository>();

        // This unit test never calls a budget operation. Avoid connecting Redis merely to resolve the recovery boundary.
        services.AddSingleton<IAtomicCacheService, InMemoryAtomicCacheService>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var recovery = scope.ServiceProvider.GetRequiredService<IPasswordRecoveryService>();

        Assert.Null(await recovery.GetAccountAsync("  OWNER@EXAMPLE.TEST  ", CancellationToken.None));
    }

    /// <summary>
    /// Uses the supplied raw keys in every environment and preserves native token protection across independent providers.
    /// </summary>
    /// <param name="environmentName">The environment that must honor the same external key pair.</param>
    /// <returns>The encrypted-token validation assertions.</returns>
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task SuppliedRawKeysProtectTokensAcrossProviders(string environmentName)
    {
        using var rsa = RSA.Create(2048);
        var configuration = CreateConfiguration();
        configuration["Authentication:SigningKey"] = rsa.ExportPkcs8PrivateKeyPem();
        configuration["Authentication:EncryptionKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        using var first = CreateCredentialProvider(configuration, environmentName);
        using var second = CreateCredentialProvider(configuration, environmentName);
        var issuing = first.GetRequiredService<IOptions<OpenIddictServerOptions>>().Value;
        var receiving = second.GetRequiredService<IOptions<OpenIddictServerOptions>>().Value;
        var signing = Assert.IsType<RsaSecurityKey>(Assert.Single(issuing.SigningCredentials).Key);
        var encryption = Assert.IsType<SymmetricSecurityKey>(Assert.Single(issuing.EncryptionCredentials).Key);
        Assert.Equal(rsa.ExportParameters(false).Modulus, signing.Parameters.Modulus);
        Assert.Equal(Convert.FromBase64String(configuration["Authentication:EncryptionKey"]!), encryption.Key);

        var handler = new JsonWebTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object> { ["sub"] = "key-roundtrip" },
            SigningCredentials = issuing.SigningCredentials[0],
            EncryptingCredentials = issuing.EncryptionCredentials[0]
        });
        var result = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            IssuerSigningKey = receiving.SigningCredentials[0].Key,
            TokenDecryptionKey = receiving.EncryptionCredentials[0].Key,
            ValidateIssuer = false,
            ValidateAudience = false
        });

        Assert.True(result.IsValid);
        Assert.Equal("key-roundtrip", result.Claims["sub"]);
    }

    /// <summary>
    /// Fails incomplete deployment credentials instead of silently generating replacement keys.
    /// </summary>
    /// <param name="environmentName">The selected deployment environment.</param>
    /// <param name="hasSigningKey">Whether a private signing key is supplied.</param>
    /// <param name="hasEncryptionKey">Whether an encryption key is supplied.</param>
    [Theory]
    [InlineData("Production", false, false)]
    [InlineData("Development", false, false)]
    [InlineData("Production", true, false)]
    [InlineData("Production", false, true)]
    [InlineData("Development", true, false)]
    [InlineData("Development", false, true)]
    public void IncompleteRawKeysCannotFallBack(string environmentName, bool hasSigningKey, bool hasEncryptionKey)
    {
        using var rsa = RSA.Create(2048);
        var configuration = CreateConfiguration();
        configuration["Authentication:SigningKey"] = hasSigningKey ? rsa.ExportPkcs8PrivateKeyPem() : null;
        configuration["Authentication:EncryptionKey"] = hasEncryptionKey
            ? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) : null;

        var exception = Assert.Throws<InvalidOperationException>(() => CreateCredentialProvider(configuration, environmentName));

        Assert.Contains("SigningKey", exception.Message, StringComparison.Ordinal);
        Assert.Contains("EncryptionKey", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE KEY", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Rejects malformed or public-only material using the installed cryptography and OpenIddict validation.
    /// </summary>
    /// <param name="invalidMaterial">The single invalid credential component.</param>
    [Theory]
    [InlineData("signing")]
    [InlineData("public-only")]
    [InlineData("weak-signing")]
    [InlineData("encryption-base64")]
    [InlineData("encryption-size")]
    public void InvalidRawKeyMaterialIsRejected(string invalidMaterial)
    {
        using var rsa = RSA.Create(invalidMaterial == "weak-signing" ? 1024 : 2048);
        var configuration = CreateConfiguration();
        configuration["Authentication:SigningKey"] = invalidMaterial switch
        {
            "signing" => "not-a-pem-key",
            "public-only" => rsa.ExportSubjectPublicKeyInfoPem(),
            _ => rsa.ExportPkcs8PrivateKeyPem()
        };
        configuration["Authentication:EncryptionKey"] = invalidMaterial switch
        {
            "encryption-base64" => "not-base64!",
            "encryption-size" => Convert.ToBase64String(new byte[16]),
            _ => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        };

        Action register = () => CreateCredentialProvider(configuration, Environments.Production);
        switch (invalidMaterial)
        {
            case "public-only":
                Assert.ThrowsAny<CryptographicException>(register);
                break;
            case "encryption-base64":
                Assert.Throws<FormatException>(register);
                break;
            case "weak-signing":
            case "encryption-size":
                Assert.Throws<InvalidOperationException>(register);
                break;
            default:
                Assert.ThrowsAny<ArgumentException>(register);
                break;
        }
    }

    /// <summary>
    /// Keeps native cookie protection independent of token keys across a simulated persistent-volume restart.
    /// </summary>
    [Fact]
    public void PersistedCookieSurvivesProviderRestartAndTokenKeyChange()
    {
        var directory = Directory.CreateTempSubdirectory("authentication-keyring-");
        try
        {
            string protectedTicket;
            using (var first = CreateCredentialProvider(CreateConfiguration(), Environments.Development, directory))
            {
                var cookie = first.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                    .Get(IdentityConstants.ApplicationScheme);
                var principal = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, "keyring-owner")], IdentityConstants.ApplicationScheme));
                protectedTicket = cookie.TicketDataFormat.Protect(new AuthenticationTicket(principal, IdentityConstants.ApplicationScheme));
            }

            // Changing the supplied token pair must not replace the separately persisted cookie keyring.
            using var second = CreateCredentialProvider(CreateConfiguration(), Environments.Development, directory);
            var options = second.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(IdentityConstants.ApplicationScheme);
            var ticket = options.TicketDataFormat.Unprotect(protectedTicket);

            Assert.NotNull(ticket);
            Assert.Equal("keyring-owner", ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Builds only native registrations without opening application database or Redis connections.
    /// </summary>
    /// <param name="configuration">The credential values for this isolated provider.</param>
    /// <param name="environmentName">The native host environment.</param>
    /// <param name="keyring">The disposable volume used only by the cookie restart test.</param>
    /// <returns>The provider owned by the caller.</returns>
    private static ServiceProvider CreateCredentialProvider(
        IConfiguration configuration,
        string environmentName,
        DirectoryInfo? keyring = null)
    {
        configuration["Authentication:Issuer"] = "https://localhost/";
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        services.AddAuthenticationInfrastructure(configuration, new TestEnvironment { EnvironmentName = environmentName });
        if (keyring is not null)
        {
            services.AddDataProtection().PersistKeysToFileSystem(keyring);
        }

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    /// <summary>
    /// Binds the standalone email section while native environment configuration overrides checked-in defaults.
    /// </summary>
    [Fact]
    public void EmailSettingsUseBaseDefaultsAndNativeEnvironmentOverrides()
    {
        var prefix = $"ContextHub_ConfigTest_{Guid.NewGuid():N}_";
        var apiKeyVariable = prefix + "EmailSettings__ApiKey";
        var senderVariable = prefix + "EmailSettings__FromEmail";
        Environment.SetEnvironmentVariable(apiKeyVariable, "test-only-sendgrid-key");
        Environment.SetEnvironmentVariable(senderVariable, "verified@example.test");
        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AuthenticationTestHostPaths.GetContentRoot())
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json")
                .AddInMemoryCollection(AuthenticationTestKeys.Create())
                .AddEnvironmentVariables(prefix)
                .Build();
            using var provider = CreateCredentialProvider(configuration, Environments.Development);
            var email = provider.GetRequiredService<IOptions<SendGridOptions>>().Value;
            using var client = provider.GetRequiredService<IHttpClientFactory>()
                .CreateClient(CoreConstants.EmailVariables.HTTP_CLIENT_NAME);

            Assert.Equal("test-only-sendgrid-key", email.ApiKey);
            Assert.Equal("verified@example.test", email.FromEmail);
            Assert.Equal("Context Hub", email.FromName);
            Assert.Equal(TimeSpan.FromSeconds(30), email.RequestTimeout);
            Assert.Equal(email.RequestTimeout, client.Timeout);
            Assert.Equal("localhost", configuration["CacheSettings:RedisSettings:Host"]);
            Assert.Equal("6379", configuration["CacheSettings:RedisSettings:Port"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(apiKeyVariable, null);
            Environment.SetEnvironmentVariable(senderVariable, null);
        }
    }

    /// <summary>
    /// Applies host-owned native cache policy without replacing Redis configuration or shared fallback defaults.
    /// </summary>
    [Fact]
    public void CacheSettingsBindNativeLifetimesAndFlagsWithoutOpeningConnections()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AuthenticationTestHostPaths.GetContentRoot())
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json")
            .AddInMemoryCollection(AuthenticationTestKeys.Create())
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Authentication"] = "Host=127.0.0.1;Port=1;Database=unused;Username=test",
                ["CacheSettings:RedisSettings:Host"] = "unavailable.invalid",
                ["CacheSettings:DefaultEntryOptions:Expiration"] = "00:02:00",
                ["CacheSettings:DefaultEntryOptions:LocalCacheExpiration"] = "00:00:45",
                ["CacheSettings:DefaultEntryOptions:Flags"] = nameof(HybridCacheEntryFlags.DisableLocalCacheRead)
            })
            .Build();
        using var provider = CreateCredentialProvider(configuration, Environments.Development);
        var cache = provider.GetRequiredService<IOptions<HybridCacheOptions>>().Value;
        var defaults = cache.DefaultEntryOptions;

        Assert.NotNull(defaults);
        Assert.Equal(TimeSpan.FromMinutes(2), defaults.Expiration);
        Assert.Equal(TimeSpan.FromSeconds(45), defaults.LocalCacheExpiration);
        Assert.Equal(HybridCacheEntryFlags.DisableLocalCacheRead, defaults.Flags);
    }

    /// <summary>
    /// Supplies only email cardinality without pretending to be a persistence integration fixture.
    /// </summary>
    /// <param name="matches">The arranged matching-account count.</param>
    private sealed class EmailCardinality(long matches)
    {
        /// <summary>
        /// Gets the number of accounts sharing one normalized address.
        /// </summary>
        public long Matches { get; } = matches;
    }

    /// <summary>
    /// Leaves every entity query connected to an unusable database so an accidental lookup fails this unit test.
    /// </summary>
    /// <param name="db">The unconnected native context.</param>
    /// <param name="count">The arranged legacy cardinality.</param>
    private sealed class AmbiguousEmailRepository(AuthenticationDbContext db, EmailCardinality count)
        : UserRepository(db, NullLogger<UserRepository>.Instance)
    {
        /// <summary>
        /// Supplies legacy cardinality and verifies that inactive duplicates are not silently excluded.
        /// </summary>
        /// <param name="predicate">The normalized-email comparison only.</param>
        /// <param name="search">Unused typed search input.</param>
        /// <param name="cancellationToken">Unused cancellation input for this completed count.</param>
        /// <returns>The arranged number of accounts.</returns>
        public override Task<long> CountAsync(
            Expression<Func<User, bool>>? predicate = null,
            SearchOptions<User>? search = null,
            CancellationToken cancellationToken = default)
        {
            Assert.NotNull(predicate);
            Assert.True(predicate.Compile()(new User { NormalizedEmail = "OWNER@EXAMPLE.TEST", IsActive = false }));

            return Task.FromResult(count.Matches);
        }
    }

    /// <summary>
    /// Supplies synthetic test credentials and settings without network or credential-file I/O.
    /// </summary>
    /// <returns>The in-memory native configuration.</returns>
    private static IConfigurationRoot CreateConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(AuthenticationTestKeys.Create())
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Authentication"] = "Host=localhost;Database=initial_options;Username=test",
            ["Authentication:ClientTokenPepper"] = Convert.ToBase64String(new byte[32]),
            ["Authentication:IntrospectionClientSecret"] = new string('x', 32)
        }).Build();

    /// <summary>
    /// Selects development registration without starting a host or persisting token keys.
    /// </summary>
    private sealed class TestEnvironment : IHostEnvironment
    {
        /// <summary>
        /// Gets or sets the environment used by native credential registration.
        /// </summary>
        public string EnvironmentName { get; set; } = Environments.Development;

        /// <summary>
        /// Gets or sets the test host name.
        /// </summary>
        public string ApplicationName { get; set; } = nameof(AuthenticationRegistrationTests);

        /// <summary>
        /// Gets or sets the content root without creating files there.
        /// </summary>
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        /// <summary>
        /// Gets or sets the file provider unused by these registration-only tests.
        /// </summary>
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
