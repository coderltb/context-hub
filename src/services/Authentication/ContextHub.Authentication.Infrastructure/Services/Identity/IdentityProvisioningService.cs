namespace ContextHub.Authentication.Infrastructure.Services.Identity;

/// <summary>
/// Creates missing framework registrations through native managers during account provisioning.
/// </summary>
/// <param name="roleManager">The native role manager.</param>
/// <param name="applicationManager">The OpenIddict client manager.</param>
/// <param name="scopeManager">The OpenIddict scope manager.</param>
/// <param name="authenticationOptions">The reviewed redirect and backchannel configuration.</param>
/// <param name="logger">The safe provisioning boundary logger.</param>
/// <param name="environment">Selects the Development-only Swagger client.</param>
/// <param name="unitOfWork">Provides the existing transaction boundary for missing framework records.</param>
public sealed class IdentityProvisioningService(
    RoleManager<IdentityRole<Guid>> roleManager,
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictScopeManager scopeManager,
    IOptions<AuthenticationOptions> authenticationOptions,
    ILogger<IdentityProvisioningService> logger,
    IHostEnvironment environment,
    IUnitOfWork unitOfWork) : IIdentityProvisioningService
{
    /// <summary>
    /// Creates protocol clients and the Owner role without creating accounts or embedding credentials in migrations.
    /// </summary>
    /// <param name="cancellationToken">Cancels provisioning.</param>
    /// <returns>The asynchronous provisioning operation.</returns>
    public async Task ProvisionAsync(CancellationToken cancellationToken)
    {
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            // Account provisioning creates missing framework records; existing registrations stay unchanged.
            if (!await roleManager.RoleExistsAsync(AuthenticationConstants.OWNER))
            {
                IdentityResult roleCreationResult;

                try
                {
                    roleCreationResult = await roleManager.CreateAsync(
                        new IdentityRole<Guid>(AuthenticationConstants.OWNER) { Id = Guid.CreateVersion7() });
                }
                catch (DbUpdateException exception) when (
                    PostgreSqlExceptionHelper.GetUniqueConstraintName(exception) == AuthenticationInfrastructureConstants.UNIQUE_ROLE_NAME_INDEX)
                {
                    // The owning UoW rolls back this race; only the caller decides whether to resubmit.
                    logger.LogWarning(
                        AuthenticationLogConstants.OPERATION_REJECTED,
                        AuthenticationInfrastructureConstants.PROVISION_IDENTITY_OPERATION,
                        AuthenticationErrorConstants.IDENTITY_PROVISIONING_CONFLICT);
                    throw new BusinessException(
                        AuthenticationErrorConstants.IDENTITY_PROVISIONING_CONFLICT,
                        AuthenticationErrorConstants.ACCOUNT_OPERATION_REJECTED,
                        BusinessErrorKind.Conflict);
                }

                if (!roleCreationResult.Succeeded)
                {
                    // Native validation can observe the same race before PostgreSQL rejects an INSERT.
                    var duplicateRole = roleCreationResult.Errors.Any(error =>
                        error.Code == AuthenticationInfrastructureConstants.NATIVE_DUPLICATE_ROLE_NAME);
                    var errorCode = duplicateRole ? AuthenticationErrorConstants.IDENTITY_PROVISIONING_CONFLICT
                        : AuthenticationErrorConstants.IDENTITY_OPERATION_REJECTED;
                    logger.LogWarning(
                        AuthenticationLogConstants.OPERATION_REJECTED,
                        AuthenticationInfrastructureConstants.PROVISION_IDENTITY_OPERATION,
                        errorCode);
                    throw new BusinessException(
                        errorCode,
                        AuthenticationErrorConstants.ACCOUNT_OPERATION_REJECTED,
                        duplicateRole ? BusinessErrorKind.Conflict : BusinessErrorKind.Invalid);
                }
            }

            // Declare the administrative scope/resource relationship through the native scope manager.
            if (await scopeManager.FindByNameAsync(AuthenticationConstants.ADMIN_SCOPE, token) is null)
            {
                await scopeManager.CreateAsync(
                    new OpenIddictScopeDescriptor
                    {
                        Name = AuthenticationConstants.ADMIN_SCOPE,
                        Resources = { AuthenticationConstants.RESOURCE }
                    },
                    token);
            }

            // The Admin UI keeps its existing OIDC, refresh and exact-redirect requirements.
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = AuthenticationConstants.ADMIN_CLIENT,
                ClientType = ClientTypes.Public,
                ConsentType = ConsentTypes.Implicit,
                DisplayName = AuthenticationInfrastructureConstants.ADMIN_CLIENT_DISPLAY_NAME,
                Permissions =
                {
                    Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.EndSession,
                    Permissions.Endpoints.Revocation, Permissions.GrantTypes.AuthorizationCode, Permissions.GrantTypes.RefreshToken,
                    Permissions.ResponseTypes.Code, Permissions.Scopes.Profile, Permissions.Prefixes.Scope + AuthenticationConstants.ADMIN_SCOPE
                },
                Requirements = { Requirements.Features.ProofKeyForCodeExchange }
            };
            descriptor.RedirectUris.UnionWith(authenticationOptions.Value.RedirectUris.Select(uri => new Uri(uri)));
            descriptor.PostLogoutRedirectUris.UnionWith(authenticationOptions.Value.PostLogoutRedirectUris.Select(uri => new Uri(uri)));
            await CreateApplicationIfMissingAsync(descriptor, token);

            // Swagger requests an access token through PKCE; it never receives a secret, ID token or refresh grant.
            if (environment.IsDevelopment())
            {
                var swagger = new OpenIddictApplicationDescriptor
                {
                    ClientId = AuthenticationConstants.SWAGGER_CLIENT,
                    ClientType = ClientTypes.Public,
                    ConsentType = ConsentTypes.Implicit,
                    DisplayName = AuthenticationInfrastructureConstants.SWAGGER_CLIENT_DISPLAY_NAME,
                    Permissions =
                    {
                        Permissions.Endpoints.Authorization, Permissions.Endpoints.Token,
                        Permissions.GrantTypes.AuthorizationCode, Permissions.ResponseTypes.Code,
                        Permissions.Prefixes.Scope + AuthenticationConstants.ADMIN_SCOPE
                    },
                    Requirements = { Requirements.Features.ProofKeyForCodeExchange }
                };
                swagger.RedirectUris.Add(new Uri(authenticationOptions.Value.Issuer.TrimEnd('/') + AuthenticationConstants.SWAGGER_CALLBACK_PATH));
                await CreateApplicationIfMissingAsync(swagger, token);
            }

            // This confidential credential belongs to the Context Hub backend introspection call, not to an end-client API token.
            if (await applicationManager.FindByClientIdAsync(AuthenticationConstants.RESOURCE, token) is null)
            {
                await applicationManager.CreateAsync(
                    new OpenIddictApplicationDescriptor
                    {
                        ClientId = AuthenticationConstants.RESOURCE,
                        ClientType = ClientTypes.Confidential,
                        ClientSecret = authenticationOptions.Value.IntrospectionClientSecret,
                        Permissions = { Permissions.Endpoints.Introspection }
                    },
                    token);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Creates a missing client registration without modifying persisted metadata.
    /// </summary>
    /// <param name="descriptor">The initial client metadata owned by this service.</param>
    /// <param name="cancellationToken">Cancels native manager operations.</param>
    /// <returns>The native client creation when no registration exists.</returns>
    private async Task CreateApplicationIfMissingAsync(
        OpenIddictApplicationDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        if (await applicationManager.FindByClientIdAsync(descriptor.ClientId!, cancellationToken) is null)
        {
            await applicationManager.CreateAsync(descriptor, cancellationToken);
        }
    }
}
