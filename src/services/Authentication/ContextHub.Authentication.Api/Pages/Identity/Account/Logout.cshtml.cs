namespace ContextHub.Authentication.Api.Pages.Identity.Account;

/// <summary>
/// Displays confirmation and forwards protocol fields to the native validated end-session POST, not a second logout engine.
/// </summary>
[AllowAnonymous]
public sealed class LogoutModel : SsoPageModel
{
    /// <summary>
    /// Gets the optional protocol client submitted for native validation.
    /// </summary>
    public string? ClientId { get; private set; }

    /// <summary>
    /// Gets the opaque end-session hint without parsing or logging it.
    /// </summary>
    public string? IdTokenHint { get; private set; }

    /// <summary>
    /// Gets the untrusted redirect submitted only to native OpenIddict allowlist validation.
    /// </summary>
    public string? PostLogoutRedirectUri { get; private set; }

    /// <summary>
    /// Gets the client-owned opaque state forwarded without reinterpretation.
    /// </summary>
    public string? State { get; private set; }

    /// <summary>
    /// Accepts only the local end-session continuation; GET never revokes tokens or signs out a cookie.
    /// </summary>
    /// <returns>The confirmation page or a safe rejected continuation.</returns>
    public override Task<IActionResult> OnGetAsync()
    {
        if (!HasLocalReturnUrl())
        {
            return Task.FromResult<IActionResult>(BadRequest());
        }

        var separator = ReturnUrl.IndexOf('?');
        var path = separator < 0 ? ReturnUrl : ReturnUrl[..separator];

        if (path != AuthenticationConstants.Endpoints.HOME_PAGE && path != AuthenticationConstants.Endpoints.LOGOUT)
        {
            return Task.FromResult<IActionResult>(BadRequest());
        }

        // These values are not authorization decisions; the native protocol endpoint validates them on POST.
        if (separator >= 0)
        {
            var fields = QueryHelpers.ParseQuery(ReturnUrl[separator..]);
            ClientId = fields[Parameters.ClientId].ToString();
            IdTokenHint = fields[Parameters.IdTokenHint].ToString();
            PostLogoutRedirectUri = fields[Parameters.PostLogoutRedirectUri].ToString();
            State = fields[Parameters.State].ToString();
        }

        return Task.FromResult<IActionResult>(Page());
    }
}
