namespace ContextHub.Authentication.Api.Pages.Identity.Account;

/// <summary>
/// Renders a generic denial without exposing account state or native authorization diagnostics.
/// </summary>
[AllowAnonymous]
public sealed class AccessDeniedModel : SsoPageModel
{
    /// <summary>
    /// Preserves the forbidden status instead of returning a misleading successful page response.
    /// </summary>
    /// <returns>The safe HTML denial.</returns>
    public override Task<IActionResult> OnGetAsync()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;

        return Task.FromResult<IActionResult>(Page());
    }
}
