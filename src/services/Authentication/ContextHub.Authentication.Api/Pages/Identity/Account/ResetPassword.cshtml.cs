namespace ContextHub.Authentication.Api.Pages.Identity.Account;

/// <summary>
/// Accepts a replacement password only after the dedicated native recovery cookie has authenticated this request.
/// </summary>
/// <param name="mediator">The Application reset use case reading identity through the shared accessor.</param>
[Authorize(AuthenticationSchemes = AuthenticationRecoveryConstants.RECOVERY_SCHEME)]
public sealed class ResetPasswordModel(IMediator mediator) : SsoPageModel
{
    /// <summary>
    /// Gets or sets only the new password and its confirmation; identity and proof never come from form fields.
    /// </summary>
    [BindProperty]
    public ResetPasswordRequestDto Input { get; set; } = new();

    /// <summary>
    /// Returns to login after atomic native reset without automatically granting a session.
    /// </summary>
    /// <param name="cancellationToken">Cancels the owned password/grant transaction.</param>
    /// <returns>Login after success, another password attempt or a fresh recovery request.</returns>
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

        var outcome = await mediator.Send(new ResetPasswordCommand(Input), cancellationToken);
        if (outcome == PasswordResetOutcome.PasswordRejected)
        {
            // Native password-policy failure leaves the short proof cookie available for correction while it remains valid.
            ModelState.AddModelError(string.Empty, AuthenticationErrorConstants.PASSWORD_REJECTED_MESSAGE);

            return Page();
        }

        await HttpContext.SignOutAsync(AuthenticationRecoveryConstants.RECOVERY_SCHEME);

        // Invalid or expired proof starts recovery again; success still requires the usual password login.
        return outcome == PasswordResetOutcome.Succeeded
            ? RedirectToPage(SsoPageConstants.LOGIN_PAGE, new { ReturnUrl })
            : RedirectToPage(SsoPageConstants.FORGOT_PAGE, new { ReturnUrl });
    }
}
