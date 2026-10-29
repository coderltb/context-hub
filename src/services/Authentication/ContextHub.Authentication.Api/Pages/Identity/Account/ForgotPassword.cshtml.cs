namespace ContextHub.Authentication.Api.Pages.Identity.Account;

/// <summary>
/// Binds recovery email while Mediator owns the use case and native services own proof and delivery.
/// </summary>
/// <param name="mediator">The Application dispatcher; no credential manager is exposed to this page.</param>
[AllowAnonymous]
public sealed class ForgotPasswordModel(IMediator mediator) : SsoPageModel
{
    /// <summary>
    /// Gets or sets the recovery address without treating it as proof of account ownership.
    /// </summary>
    [BindProperty]
    public ForgotPasswordRequestDto Input { get; set; } = new();

    /// <summary>
    /// Continues identically for unavailable accounts, send rejection and accepted email.
    /// </summary>
    /// <param name="cancellationToken">Cancels coordination, account queries and mail delivery.</param>
    /// <returns>The OTP form or the current safe validation form.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!HasLocalReturnUrl())
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await mediator.Send(new ForgotPasswordCommand(Input), cancellationToken);

        // Email is a form convenience, not reset authority; the next step still requires native proof.
        return RedirectToPage(SsoPageConstants.VERIFY_PAGE, new { email = Input.Email, ReturnUrl });
    }
}
