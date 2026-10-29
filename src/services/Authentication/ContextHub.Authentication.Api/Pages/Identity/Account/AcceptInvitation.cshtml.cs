namespace ContextHub.Authentication.Api.Pages.Identity.Account;

/// <summary>
/// Accepts passwords only in the antiforgery-protected SSO invitation form.
/// </summary>
/// <param name="mediator">The Application dispatcher for interactive account operations.</param>
[AllowAnonymous]
public sealed class AcceptInvitationModel(IMediator mediator) : SsoPageModel
{
    /// <summary>
    /// Gets or sets the public account identifier bound into the invitation.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the native one-time invitation proof; never log it.
    /// </summary>
    [BindProperty(SupportsGet = true), Required]
    public string Token { get; set; } = "";

    /// <summary>
    /// Gets or sets the initial password input without sending it through an Admin JSON request.
    /// </summary>
    [BindProperty]
    public InvitationInputModel Input { get; set; } = new();

    /// <summary>
    /// Sets the initial password and resumes the validated local SSO continuation.
    /// </summary>
    /// <param name="cancellationToken">Cancels the invitation lookup before native Identity password handling.</param>
    /// <returns>The local continuation or the same safe failure for every unusable invitation.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!HasLocalReturnUrl())
        {
            return BadRequest();
        }

        if (ModelState.IsValid && await mediator.Send(new AcceptInvitationCommand(new(UserId, Token, Input.Password)), cancellationToken))
        {
            return LocalRedirect(ReturnUrl);
        }

        ModelState.AddModelError(string.Empty, AuthenticationErrorConstants.INVITATION_UNAVAILABLE);

        return Page();
    }
}
