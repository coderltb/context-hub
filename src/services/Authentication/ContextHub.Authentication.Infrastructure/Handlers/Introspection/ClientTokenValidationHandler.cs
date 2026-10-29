namespace ContextHub.Authentication.Infrastructure.Handlers.Introspection;

/// <summary>
/// Adds application credentials only to native introspection; other grants retain OpenIddict validation.
/// </summary>
/// <param name="clientService">The feature service loading Authentication-owned credential state.</param>
public sealed class ClientTokenValidationHandler(IClientService clientService)
    : IOpenIddictServerHandler<ValidateTokenContext>
{
    /// <summary>
    /// Attaches a principal only after all application credential checks succeed.
    /// </summary>
    /// <param name="context">The native token validation operation.</param>
    /// <returns>The asynchronous validation operation.</returns>
    public async ValueTask HandleAsync(ValidateTokenContext context)
    {
        if (context.Transaction.EndpointType != OpenIddictServerEndpointType.Introspection ||
            !context.Token.StartsWith(AuthenticationConstants.CLIENT_TOKEN_PREFIX, StringComparison.Ordinal))
        {
            return;
        }

        // The feature service checks format before IO, then verifies live persisted state and the full digest.
        var token = await clientService.ValidateTokenAsync(context.Token, context.CancellationToken);
        if (token is null)
        {
            context.Reject(Errors.InvalidToken);

            return;
        }

        // ClientToken remains authoritative; native introspection checks usage, expiry and audience.
        var principal = new ClaimsPrincipal(new ClaimsIdentity(TokenTypes.Bearer, Claims.Name, Claims.Role));
        principal.SetClaim(Claims.Subject, AuthenticationConstants.CLIENT_SUBJECT_PREFIX + token.ClientId.ToString("D"));
        principal.SetClaim(AuthenticationConstants.ACTOR_TYPE, AuthenticationConstants.CLIENT_ACTOR);
        principal.SetClaim(Claims.ClientId, token.ClientId.ToString("D"));
        principal.SetClaim(AuthenticationConstants.CREDENTIAL_ID, token.TokenId.ToString("D"));
        principal.SetTokenType(TokenTypeIdentifiers.AccessToken);
        principal.SetCreationDate(token.CreatedAt);
        principal.SetExpirationDate(token.ExpiresAt);

        // Keep this token's exact allowlist; a client credential never inherits the issuing administrator's role.
        principal.SetClaims(AuthenticationConstants.ALLOWED_TOOL, token.AllowedTools.ToImmutableArray());
        principal.SetScopes(AuthenticationConstants.MCP_SCOPE);
        principal.SetAudiences(AuthenticationConstants.RESOURCE);
        context.Principal = principal;
    }
}
