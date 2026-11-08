namespace ContextHub.Authentication.Api.Pages;

/// <summary>
/// Shows the native browser session and the configured Console destination.
/// </summary>
/// <param name="authenticationOptions">The deployment-owned Console address.</param>
/// <param name="environment">The environment allowing local HTTP during development.</param>
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IndexModel(
    IOptions<AuthenticationServiceOptions> authenticationOptions,
    IWebHostEnvironment environment) : PageModel
{
    /// <summary>
    /// Gets whether the Identity application cookie authenticated successfully.
    /// </summary>
    public bool IsSignedIn { get; private set; }

    /// <summary>
    /// Gets the configured web destination, or null when none is usable.
    /// </summary>
    public string? ConsoleUrl { get; private set; }

    /// <summary>
    /// Reads the cookie with native stamp validation before displaying session state.
    /// </summary>
    /// <returns>The compiled SSO home page.</returns>
    public async Task OnGetAsync()
    {
        var authentication = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        IsSignedIn = authentication.Succeeded;

        // The destination comes from deployment configuration, never a caller-supplied return URL.
        if (Uri.TryCreate(authenticationOptions.Value.ConsoleUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || environment.IsDevelopment() && uri.Scheme == Uri.UriSchemeHttp))
        {
            ConsoleUrl = uri.AbsoluteUri;
        }
    }
}
