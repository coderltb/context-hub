namespace ContextHub.Authentication.Api.Controllers.Oidc;

/// <summary>
/// Adapts native cookie and protocol authentication to Application grant decisions without accepting a password grant.
/// </summary>
/// <param name="mediator">The Application dispatcher; native HTTP results remain in this adapter.</param>
[ApiExplorerSettings(IgnoreApi = true)]
[Route(AuthenticationConstants.Endpoints.CONNECT_PREFIX)]
public sealed class AuthorizationController(IMediator mediator) : Controller
{
    /// <summary>
    /// Challenges anonymous callers and issues codes only from currently permitted Owner sessions.
    /// </summary>
    /// <param name="cancellationToken">Cancels Application grant validation and persistence.</param>
    /// <returns>A cookie challenge, protocol rejection or native sign-in.</returns>
    [HttpGet(AuthenticationConstants.Endpoints.AUTHORIZE_PATH)]
    [TypeFilter(typeof(OidcAuthenticationFilter), Arguments = [OidcAuthenticationOperation.Authorize])]
    public async Task<IActionResult> Authorize(CancellationToken cancellationToken)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException(AuthenticationErrorConstants.OIDC_REQUEST_UNAVAILABLE);
        var grant = new AuthorizationGrantRequestModel(
            request.ClientId,
            [.. request.GetScopes()],
            request.HasScope(AuthenticationConstants.ADMIN_SCOPE) &&
                !string.IsNullOrEmpty(request.State) && request.CodeChallengeMethod == CodeChallengeMethods.Sha256 &&
                (request.ClientId == AuthenticationConstants.SWAGGER_CLIENT
                    ? request.GetScopes().Length == 1
                    : request.HasScope(Scopes.OpenId) && !string.IsNullOrEmpty(request.Nonce)));
        var result = await mediator.Send(new CreateAuthorizationGrantCommand(grant), cancellationToken);

        // Application decides current eligibility; OpenIddict still owns issuance and its protocol response body.
        return result.Decision switch
        {
            AuthorizationGrantDecision.Granted when result.Principal is not null =>
                SignIn(result.Principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme),
            AuthorizationGrantDecision.RequiresSignIn => Challenge(
                new AuthenticationProperties { RedirectUri = Request.Path + Request.QueryString },
                IdentityConstants.ApplicationScheme),
            _ => Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)
        };
    }

    /// <summary>
    /// Exchanges only native-authenticated authorization codes and rotating refresh tokens.
    /// </summary>
    /// <param name="cancellationToken">Cancels the current Owner eligibility query.</param>
    /// <returns>The native token response or a protocol rejection.</returns>
    [HttpPost(AuthenticationConstants.Endpoints.TOKEN_PATH)]
    [TypeFilter(typeof(OidcAuthenticationFilter), Arguments = [OidcAuthenticationOperation.Exchange])]
    public async Task<IActionResult> Exchange(CancellationToken cancellationToken)
    {
        if (!await mediator.Send(new ValidateTokenGrantQuery(), cancellationToken))
        {
            return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // Redemption, rotation, reuse leeway and token-entry persistence remain in OpenIddict's sign-in pipeline.
        return SignIn(User, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Displays a local confirmation rather than mutating session state on a GET request.
    /// </summary>
    /// <returns>The antiforgery-protected local confirmation page.</returns>
    [HttpGet(AuthenticationConstants.Endpoints.LOGOUT_PATH)]
    public IActionResult Logout() =>
        Redirect(QueryHelpers.AddQueryString(
            AuthenticationConstants.Endpoints.LOGOUT_PAGE,
            AuthenticationConstants.RETURN_URL_PARAMETER,
            Request.Path + Request.QueryString));

    /// <summary>
    /// Revokes the confirmed current session before native local and protocol sign-out.
    /// </summary>
    /// <param name="cancellationToken">Cancels session-family revocation.</param>
    /// <returns>The native end-session response with its validated redirect behavior.</returns>
    [HttpPost(AuthenticationConstants.Endpoints.LOGOUT_PATH)]
    [ValidateAntiForgeryToken]
    [TypeFilter(typeof(OidcAuthenticationFilter), Arguments = [OidcAuthenticationOperation.Logout])]
    public async Task<IActionResult> LogoutConfirmed(CancellationToken cancellationToken)
    {
        await mediator.Send(new RevokeSessionCommand(), cancellationToken);

        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        var request = HttpContext.GetOpenIddictServerRequest();

        // OpenIddict owns validated post-logout destinations; the local fallback never overrides a protocol redirect.
        return SignOut(
            new AuthenticationProperties
            {
                RedirectUri = string.IsNullOrEmpty(request?.PostLogoutRedirectUri)
                    ? AuthenticationConstants.Endpoints.HOME_PAGE
                    : null
            },
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
