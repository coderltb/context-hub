namespace ContextHub.Authentication.Api.Pages.Identity.Account;

/// <summary>
/// Exposes no exception message, stack, account state or submitted credentials in SSO failures.
/// </summary>
[AllowAnonymous]
public sealed class SsoErrorModel : SsoPageModel
{
    /// <summary>
    /// Preserves the failure status while rendering only the safe HTML message.
    /// </summary>
    /// <returns>The generic error page.</returns>
    public override Task<IActionResult> OnGetAsync()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;

        return Task.FromResult<IActionResult>(Page());
    }
}
