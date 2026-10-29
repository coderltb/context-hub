namespace ContextHub.Authentication.Infrastructure.DependencyInjection;

/// <summary>
/// Composes this service's native identity, persistence and scheduler dependencies in one place.
/// </summary>
public static class ServiceRegistration
{
    /// <summary>
    /// Registers service-owned authentication without opening a database or provisioning accounts during host startup.
    /// </summary>
    /// <param name="services">The host container.</param>
    /// <param name="configuration">Configuration including externally supplied secrets.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <returns>The same container for API composition.</returns>
    public static IServiceCollection AddAuthenticationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.TryAddSingleton(environment);

        // Bind the Authentication section through native options; environment variables and User Secrets remain configuration providers owned by the host.
        services.AddConfiguredOptions<AuthenticationOptions>(
            configuration,
            AuthenticationInfrastructureConstants.SETTINGS_SECTION);

        // Compose one persistence scope before native managers and feature services use it; registration does not provision schema or accounts.
        AddPersistence(services, configuration);
        AddIdentity(services);
        ConfigureSecurityStampValidation(services);
        ConfigureApplicationCookie(services);
        AddProtocol(services, configuration);

        // Keep interactive recovery/email and live authorization registration separate from the protocol token handlers.
        AddRecoveryAndEmail(services, configuration);
        AddCaching(services, configuration);
        AddFeatureServices(services);
        AddAuthorizationPolicies(services);
        AddDependencyHealthChecks(services);

        return services;
    }

    /// <summary>
    /// Connects native EF stores to the existing DB-first schema; registration never migrates or opens a connection.
    /// </summary>
    /// <param name="services">The service container owning one scoped context and UoW.</param>
    /// <param name="configuration">The host configuration supplying the named database connection.</param>
    private static void AddPersistence(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<UserSaveChangesInterceptor>();

        // Shared registration appends audit stamping after the service's User interceptor in the same context scope.
        services.AddAuditedDbContext<AuthenticationDbContext>((provider, options) =>
            options.UseNpgsql(configuration.GetConnectionString(AuthenticationInfrastructureConstants.DATABASE_CONNECTION))
                .UseOpenIddict<Guid>()
                .AddInterceptors(provider.GetRequiredService<UserSaveChangesInterceptor>()));
        services.AddPersistence<AuthenticationDbContext>();
    }

    /// <summary>
    /// Configures framework managers and stores without replacing native password or invitation handling.
    /// </summary>
    /// <param name="services">The service container.</param>
    private static void AddIdentity(IServiceCollection services)
    {
        // Use native user/role stores and managers; these settings select policy, not a custom password or token implementation.
        services.AddIdentity<User, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;

            // Select the numeric native email proof for password recovery; invitation proofs use a separate provider and purpose.
            options.Tokens.PasswordResetTokenProvider = TokenOptions.DefaultEmailProvider;

            // Match the existing DB-first Identity model instead of adding unused framework tables at startup.
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
            options.Stores.MaxLengthForKeys = AuthenticationInfrastructureConstants.MAX_IDENTITY_KEY_LENGTH;

            // Apply password and lockout limits inside Identity so Razor and administrative creation share the same native policy.
            options.Password.RequiredLength = AuthenticationInfrastructureConstants.REQUIRED_PASSWORD_LENGTH;
            options.Lockout.MaxFailedAccessAttempts = AuthenticationInfrastructureConstants.MAX_FAILED_ACCESS_ATTEMPTS;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(AuthenticationInfrastructureConstants.LOCKOUT_MINUTES);
        }).AddEntityFrameworkStores<AuthenticationDbContext>().AddDefaultTokenProviders()
            .AddTokenProvider<DataProtectorTokenProvider<User>>(AuthenticationConstants.INVITATION_PROVIDER);

        // This lifetime bounds Data Protection invitation proofs; it does not override the email OTP provider's native time window.
        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromMinutes(AuthenticationInfrastructureConstants.INVITATION_LIFETIME_MINUTES));
    }

    /// <summary>
    /// Revalidates account state while retaining only the authenticated session binding and method.
    /// </summary>
    /// <param name="services">The container holding native Identity security-stamp validation.</param>
    private static void ConfigureSecurityStampValidation(IServiceCollection services)
    {
        services.Configure<SecurityStampValidatorOptions>(options =>
        {
            // A password/stamp change must reject the next cookie use instead of leaving a thirty-minute grant-issuance window.
            options.ValidationInterval = TimeSpan.Zero;
            options.OnRefreshingPrincipal = context =>
            {
                if (context.NewPrincipal is { } principal)
                {
                    // Identity has verified the stamp and rebuilt current roles. Retain only the protected session binding and completed method.
                    principal.SetClaim(AuthenticationConstants.SESSION_ID,
                        context.CurrentPrincipal?.FindFirstValue(AuthenticationConstants.SESSION_ID));
                    principal.SetClaim(AuthenticationConstants.AUTHENTICATION_METHOD_CLAIM,
                        context.CurrentPrincipal?.FindFirstValue(AuthenticationConstants.AUTHENTICATION_METHOD_CLAIM));
                }

                return Task.CompletedTask;
            };
        });
    }

    /// <summary>
    /// Keeps service-owned SSO cookie transport, session validation and lifetime settings together.
    /// </summary>
    /// <param name="services">The container holding the native Identity cookie scheme.</param>
    private static void ConfigureApplicationCookie(IServiceCollection services)
    {
        services.ConfigureApplicationCookie(options =>
        {
            options.Events.OnValidatePrincipal = async context =>
            {
                await SecurityStampValidator.ValidatePrincipalAsync(context);
                if (context.Principal is not { Identity.IsAuthenticated: true } principal)
                {
                    return;
                }

                // A valid account stamp does not revive a browser session that has explicitly logged out.
                var sessionService = context.HttpContext.RequestServices.GetRequiredService<ISessionService>();
                if (!await sessionService.IsSessionActiveAsync(
                    principal.FindFirstValue(ClaimTypes.NameIdentifier),
                    principal.FindFirstValue(AuthenticationConstants.SESSION_ID),
                    context.HttpContext.RequestAborted))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                }
            };

            // Use a service-specific host cookie with native transport protections; it is not the credential issued to application clients.
            options.Cookie.Name = AuthenticationInfrastructureConstants.SSO_COOKIE;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.LoginPath = AuthenticationConstants.Endpoints.LOGIN_PAGE;
            options.AccessDeniedPath = AuthenticationConstants.Endpoints.ACCESS_DENIED_PAGE;

            // Keep a fixed browser-session lifetime; refresh-token lifetime is a separate setting in the Authentication section.
            options.ExpireTimeSpan = TimeSpan.FromHours(AuthenticationInfrastructureConstants.SSO_LIFETIME_HOURS);
            options.SlidingExpiration = false;
        });
    }

    /// <summary>
    /// Registers native protocol validation and cleanup; only service-owned client credentials need an introspection adapter.
    /// </summary>
    /// <param name="services">The service container.</param>
    /// <param name="configuration">The host's external deployment configuration.</param>
    private static void AddProtocol(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // Pass deployment issuer and human refresh lifetime from the bound options into native OpenIddict settings.
        services.AddOptions<OpenIddictServerOptions>().Configure<IOptions<AuthenticationOptions>>((options, configured) =>
        {
            options.Issuer = new Uri(configured.Value.Issuer, UriKind.Absolute);
            options.RefreshTokenLifetime = TimeSpan.FromHours(configured.Value.RefreshTokenLifetimeHours);
        });

        services.AddOpenIddict().AddCore(options =>
        {
            options.UseEntityFrameworkCore().UseDbContext<AuthenticationDbContext>().ReplaceDefaultEntities<Guid>();

            // Read current token/authorization state from the native store so revocation is not hidden by the entity cache.
            options.DisableEntityCaching();

            // The native integration registers its single cleanup job and scheduler. No second AddQuartz scan is needed.
            options.UseQuartz();
        }).AddServer(options =>
        {
            // Expose only the protocol endpoints this service implements; REST envelope adapters do not own these responses.
            options.SetAuthorizationEndpointUris(AuthenticationConstants.Endpoints.AUTHORIZE)
                .SetTokenEndpointUris(AuthenticationConstants.Endpoints.TOKEN)
                .SetRevocationEndpointUris(AuthenticationConstants.Endpoints.REVOCATION)
                .SetEndSessionEndpointUris(AuthenticationConstants.Endpoints.LOGOUT)
                .SetIntrospectionEndpointUris(AuthenticationConstants.Endpoints.INTROSPECTION);

            // Keep human sign-in on code + PKCE with refresh; no password grant or end-client credential exchange is enabled here.
            options.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow().RequireProofKeyForCodeExchange();

            // Register the server scope vocabulary; grant-building and introspection decide which approved scopes enter each principal.
            options.RegisterScopes(
                Scopes.OpenId,
                Scopes.Profile,
                Scopes.OfflineAccess,
                AuthenticationConstants.ADMIN_SCOPE,
                AuthenticationConstants.MCP_SCOPE);
            options.SetAccessTokenLifetime(TimeSpan.FromMinutes(AuthenticationInfrastructureConstants.ACCESS_TOKEN_LIFETIME_MINUTES));

            // Refresh belongs to native human sessions; client-issued credentials use their separate persisted lifecycle.
            options.UseReferenceRefreshTokens();
            ConfigureCredentials(options, configuration);

            options.UseAspNetCore().EnableAuthorizationEndpointPassthrough().EnableTokenEndpointPassthrough()
                .EnableEndSessionEndpointPassthrough();

            // Restrict the custom opaque-token adapter to its existing validation boundary; native handlers still validate human protocol tokens.
            options.AddEventHandler<ValidateTokenContext>(builder => builder
                .UseScopedHandler<ClientTokenValidationHandler>()
                .SetOrder(
                    Protection.ValidateIdentityModelToken.Descriptor.Order -
                    AuthenticationInfrastructureConstants.CLIENT_TOKEN_HANDLER_ORDER_OFFSET));
            options.AddEventHandler<HandleIntrospectionRequestContext>(builder => builder
                .UseScopedHandler<CurrentUserIntrospectionHandler>()
                .SetOrder(
                    Introspection.AttachApplicationClaims.Descriptor.Order +
                    AuthenticationInfrastructureConstants.INTROSPECTION_HANDLER_ORDER_OFFSET));
        }).AddValidation(options =>
        {
            // Validate Admin API tokens against this service's native server and current store entries.
            options.UseLocalServer();
            options.UseAspNetCore();
            options.EnableTokenEntryValidation();
            options.EnableAuthorizationEntryValidation();
        });

        // Start the native cleanup scheduler with the host and wait for running cleanup work during graceful shutdown.
        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);
    }

    /// <summary>
    /// Registers raw token keys without replacing the native cookie and invitation keyring.
    /// </summary>
    /// <param name="server">The native OpenIddict server builder.</param>
    /// <param name="configuration">The externally supplied private signing and encryption keys.</param>
    private static void ConfigureCredentials(
        OpenIddictServerBuilder server,
        IConfiguration configuration)
    {
        // Keep cookie persistence native; container deployments preserve and protect the profile keyring volume.
        server.Services.AddDataProtection()
            .SetApplicationName(AuthenticationInfrastructureConstants.DATA_PROTECTION_APPLICATION);
        var section = configuration.GetRequiredSection(AuthenticationInfrastructureConstants.SETTINGS_SECTION);
        var signingKey = section[nameof(AuthenticationOptions.SigningKey)];
        var encryptionKey = section[nameof(AuthenticationOptions.EncryptionKey)];

        // Every environment uses the supplied pair; missing configuration must not generate replacement keys.
        if (string.IsNullOrWhiteSpace(signingKey) || string.IsNullOrWhiteSpace(encryptionKey))
        {
            throw new InvalidOperationException(AuthenticationInfrastructureErrorConstants.TOKEN_KEYS_REQUIRED);
        }

        // Export private parameters so OpenIddict does not retain the temporary RSA instance after disposal.
        using var rsa = RSA.Create();
        rsa.ImportFromPem(signingKey);

        // Reject weak RSA keys before any token can be issued.
        if (rsa.KeySize < AuthenticationInfrastructureConstants.MIN_RSA_SIGNING_KEY_BITS)
        {
            throw new InvalidOperationException(string.Format(
                AuthenticationInfrastructureErrorConstants.SIGNING_KEY_TOO_SMALL,
                AuthenticationInfrastructureConstants.MIN_RSA_SIGNING_KEY_BITS));
        }

        server.AddSigningKey(new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: true)));
        server.AddEncryptionKey(new SymmetricSecurityKey(Convert.FromBase64String(encryptionKey)));
    }

    /// <summary>
    /// Registers the isolated recovery cookie, feature port and shared email transport.
    /// </summary>
    /// <param name="services">The host container owning clients and feature scopes.</param>
    /// <param name="configuration">External deployment options; no secret is copied into source or diagnostics.</param>
    private static void AddRecoveryAndEmail(IServiceCollection services, IConfiguration configuration)
    {
        // Protect the temporary password-reset proof in a dedicated cookie scheme; it carries no Owner role or Admin grant.
        services.AddAuthentication().AddCookie(AuthenticationRecoveryConstants.RECOVERY_SCHEME, options =>
        {
            options.Cookie.Name = AuthenticationRecoveryConstants.RECOVERY_COOKIE;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(AuthenticationRecoveryConstants.COOKIE_LIFETIME_MINUTES);
            options.SlidingExpiration = false;
            options.LoginPath = AuthenticationRecoveryConstants.FORGOT_PAGE;
            options.AccessDeniedPath = AuthenticationConstants.Endpoints.ACCESS_DENIED_PAGE;
        });

        // Bind host-owned email settings separately from account policy; templates remain with the recovery workflow.
        services.AddSendGridEmail(configuration.GetSection(
            AuthenticationInfrastructureConstants.EMAIL_SETTINGS_SECTION));
        services.AddScoped<IPasswordRecoveryService, PasswordRecoveryService>();
    }

    /// <summary>
    /// Registers local role metadata caching and the shared Redis pool for explicit security coordination.
    /// </summary>
    /// <param name="services">The container owning cache and connection lifetimes.</param>
    /// <param name="configuration">The host's cache lifetimes and structured Redis settings.</param>
    private static void AddCaching(IServiceCollection services, IConfiguration configuration)
    {
        // Roles stay in L1; recovery budgets use Redis coordination without enabling a distributed payload cache.
        services.AddCache(options => configuration.GetSection(
            AuthenticationInfrastructureConstants.CACHE_SETTINGS_SECTION).Bind(options));
        services.AddRedisConnection(configuration.GetSection(
            AuthenticationInfrastructureConstants.REDIS_SETTINGS_SECTION));
        services.AddRedisCoordination();
    }

    /// <summary>
    /// Wires feature ports and the current Owner policy without embedding account workflows in DI.
    /// </summary>
    /// <param name="services">The service container.</param>
    private static void AddFeatureServices(IServiceCollection services)
    {
        // Feature ports resolve through one request scope, so specific repositories, native managers and UoW share the same context.
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IIdentityProvisioningService, IdentityProvisioningService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IOwnerRegistrationService, OwnerRegistrationService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IAccountService, AccountService>();
    }

    /// <summary>
    /// Registers the native current-Owner policy separately from feature service lifetimes.
    /// </summary>
    /// <param name="services">The host container holding the native validation scheme.</param>
    private static void AddAuthorizationPolicies(IServiceCollection services)
    {
        // Recheck current Owner state through the native authorization handler; stale role claims alone must not authorize an administrative request.
        services.AddScoped<IAuthorizationHandler, CurrentOwnerHandler>();
        services.AddAuthorization(options => options.AddPolicy(
            AuthenticationConstants.OWNER_POLICY,
            policy =>
        {
            policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.RequireClaim(AuthenticationConstants.ACTOR_TYPE, AuthenticationConstants.USER_ACTOR);
            policy.RequireRole(AuthenticationConstants.OWNER);
            policy.RequireAssertion(context => context.User.HasScope(AuthenticationConstants.ADMIN_SCOPE));
            policy.AddRequirements(new CurrentOwnerRequirement());
        }));
    }

    /// <summary>
    /// Registers connectivity probes without creating a second Redis pool or including recovery in database readiness.
    /// </summary>
    /// <param name="services">The container owning the scoped database context and shared Redis multiplexer.</param>
    private static void AddDependencyHealthChecks(IServiceCollection services)
    {
        // EF checks connectivity only. Schema compatibility remains the responsibility of DB-first integration tests.
        services.AddHealthChecks()
            .AddDbContextCheck<AuthenticationDbContext>(
                AuthenticationInfrastructureConstants.POSTGRESQL_HEALTH_CHECK,
                tags: [AuthenticationInfrastructureConstants.READINESS_HEALTH_TAG])
            .AddRedis(
                provider => provider.GetRequiredService<IConnectionMultiplexer>(),
                name: AuthenticationInfrastructureConstants.REDIS_HEALTH_CHECK,
                tags: [AuthenticationInfrastructureConstants.RECOVERY_HEALTH_TAG]);
    }
}
