namespace ContextHub.Authentication.Api.Filters;

/// <summary>
/// Selects exactly one native-authenticated identity before Mediator and the existing current-user accessor run.
/// </summary>
/// <param name="operation">The native identity source required by this OIDC action.</param>
public sealed class OidcAuthenticationFilter(OidcAuthenticationOperation operation) : IAsyncActionFilter
{
    /// <summary>
    /// Replaces the ambient principal without merging claims or accepting an identity from a different scheme.
    /// </summary>
    /// <param name="context">The current HTTP action and native protocol context.</param>
    /// <param name="next">The action continuation after authentication.</param>
    /// <returns>The native challenge/rejection or the authenticated action execution.</returns>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var scheme = operation switch
        {
            OidcAuthenticationOperation.Authorize or OidcAuthenticationOperation.Logout => IdentityConstants.ApplicationScheme,
            OidcAuthenticationOperation.Exchange => OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            _ => null
        };

        // Clear the ambient identity first: a failed selected scheme must never inherit a successful unrelated one.
        http.User = new ClaimsPrincipal(new ClaimsIdentity());
        if (scheme is null)
        {
            context.Result = new ForbidResult(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            return;
        }

        // Accept only the enabled code/refresh grants at exchange; unrelated ambient cookies cannot authenticate this protocol operation.
        if (operation == OidcAuthenticationOperation.Exchange)
        {
            var request = http.GetOpenIddictServerRequest();
            if (request is null || (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType()))
            {
                context.Result = new ForbidResult(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                return;
            }
        }

        // Authenticate the selected native scheme once, then expose only that principal to IAuthService and the Mediator handler.
        var authentication = await http.AuthenticateAsync(scheme);
        if (authentication.Succeeded && authentication.Principal is { } principal &&
            principal.Identities.Any(identity => identity.IsAuthenticated))
        {
            http.User = principal;
        }
        else if (operation != OidcAuthenticationOperation.Logout)
        {
            context.Result = operation == OidcAuthenticationOperation.Authorize
                ? new ChallengeResult(IdentityConstants.ApplicationScheme, new AuthenticationProperties
                {
                    RedirectUri = http.Request.Path + http.Request.QueryString
                })
                : new ForbidResult(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            return;
        }

        // Logout may continue without a cookie; grant creation/exchange reaches the handler only after the selected scheme succeeds.
        await next();
    }
}
