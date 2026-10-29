namespace ContextHub.Authentication.Api.Pages.Identity.Account;

/// <summary>
/// Binds the password form while native Identity and the account service own verification and lockout.
/// </summary>
/// <param name="mediator">The Application dispatcher for interactive account operations.</param>
[AllowAnonymous]
public sealed class LoginModel(IMediator mediator) : SsoPageModel
{
    /// <summary>
    /// Gets or sets the secret form input; never log or return this model through an API.
    /// </summary>
    [BindProperty]
    public LoginRequestDto Input { get; set; } = new();

    /// <summary>
    /// Continues the validated local SSO request after successful password authentication.
    /// </summary>
    /// <param name="cancellationToken">Cancels account dispatch.</param>
    /// <returns>A safe rejected form or the next local SSO step.</returns>
    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!HasLocalReturnUrl())
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await mediator.Send(new LoginCommand(Input), cancellationToken);

        // A completed native password check establishes the SSO session; only local continuations are accepted.
        if (result)
        {
            return LocalRedirect(ReturnUrl);
        }

        ModelState.AddModelError(string.Empty, AuthenticationErrorConstants.SIGN_IN_FAILED);

        return Page();
    }
}
