namespace ContextHub.Authentication.Api.Pages.Shared;

/// <summary>
/// Shares local navigation and secret-bearing response policy across the service's SSO forms only.
/// </summary>
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class SsoPageModel : PageModel
{
    /// <summary>
    /// Gets or sets the local destination retained across credential steps.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; } = AuthenticationConstants.Endpoints.HOME_PAGE;

    /// <summary>
    /// Renders a form only when its continuation cannot redirect to an external host.
    /// </summary>
    /// <returns>The SSO page or a safe rejected continuation.</returns>
    public virtual Task<IActionResult> OnGetAsync() =>
        Task.FromResult<IActionResult>(HasLocalReturnUrl() ? Page() : BadRequest());

    /// <summary>
    /// Uses native URL validation rather than interpreting consumer-supplied hosts or URL prefixes.
    /// </summary>
    /// <returns>Whether the bound continuation remains inside this application.</returns>
    [MemberNotNull(nameof(ReturnUrl))]
    protected bool HasLocalReturnUrl()
    {
        // The first GET may omit a continuation; any supplied destination still passes native local-URL validation.
        ReturnUrl ??= AuthenticationConstants.Endpoints.HOME_PAGE;

        return Url.IsLocalUrl(ReturnUrl);
    }
}
