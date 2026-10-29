namespace ContextHub.Authentication.Api.Pages.Identity.Account;

/// <summary>
/// Exchanges verified native email proof for a dedicated short-lived protected cookie without creating an Owner session.
/// </summary>
/// <param name="mediator">The Application proof-verification use case.</param>
[AllowAnonymous]
public sealed class VerifyPasswordResetOtpModel(IMediator mediator) : SsoPageModel
{
    /// <summary>
    /// Gets or sets the address carried between forms; it is never trusted as verified identity.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the submitted address and native numeric proof.
    /// </summary>
    [BindProperty]
    public VerifyPasswordResetOtpRequestDto Input { get; set; } = new();

    /// <summary>
    /// Prefills only the editable address while preserving the shared local-return-url check.
    /// </summary>
    /// <returns>The OTP form or a rejected non-local continuation.</returns>
    public override Task<IActionResult> OnGetAsync()
    {
        Input.Email = Email ?? "";

        return base.OnGetAsync();
    }

    /// <summary>
    /// Stores only the verified account and native proof in the encrypted recovery scheme.
    /// </summary>
    /// <param name="cancellationToken">Cancels coordination and current-account reads.</param>
    /// <returns>The protected reset form or a generic rejected-code form.</returns>
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

        var grant = await mediator.Send(new VerifyPasswordResetOtpCommand(Input), cancellationToken);
        if (grant is null)
        {
            ModelState.AddModelError(string.Empty, AuthenticationErrorConstants.INVALID_PROOF_MESSAGE);

            return Page();
        }

        // This allowlist deliberately contains no Owner role, scope or full-login cookie material.
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, grant.UserId.ToString()),
            new Claim(AuthenticationConstants.ACTOR_TYPE, AuthenticationRecoveryConstants.RECOVERY_ACTOR),
            new Claim(AuthenticationRecoveryConstants.RESET_TOKEN_CLAIM, grant.Token)
        ], AuthenticationRecoveryConstants.RECOVERY_SCHEME);
        await HttpContext.SignInAsync(AuthenticationRecoveryConstants.RECOVERY_SCHEME, new ClaimsPrincipal(identity));

        return RedirectToPage(SsoPageConstants.RESET_PAGE, new { ReturnUrl });
    }
}
