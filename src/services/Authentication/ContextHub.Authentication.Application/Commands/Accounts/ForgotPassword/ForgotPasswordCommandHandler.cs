namespace ContextHub.Authentication.Application.Commands.Accounts.ForgotPassword;

/// <summary>
/// Coordinates the recovery use case while keeping native credential IO behind the Application-owned port.
/// </summary>
/// <param name="passwordRecoveryService">The native credential and provider boundary.</param>
public sealed class ForgotPasswordCommandHandler(IPasswordRecoveryService passwordRecoveryService)
    : IRequestHandler<ForgotPasswordCommand, bool>
{
    /// <summary>
    /// Applies the form workflow without returning credential diagnostics or creating an Owner session.
    /// </summary>
    /// <param name="request">The typed form input, never a log payload.</param>
    /// <param name="cancellationToken">Cancels downstream operations.</param>
    /// <returns>The safe continuation or outcome.</returns>
    public async ValueTask<bool> Handle(
        ForgotPasswordCommand request,
        CancellationToken cancellationToken)
    {
        // Apply the same budget to registered and unknown addresses before revealing any account-dependent outcome.
        if (await passwordRecoveryService.ReserveRequestAsync(request.Request, cancellationToken))
        {
            var account = await passwordRecoveryService.GetAccountAsync(request.Request.Email, cancellationToken);
            if (account is not null)
            {
                await passwordRecoveryService.SendOtpAsync(account, cancellationToken);
            }
        }

        // False acceptance, unavailable accounts and cooldown all lead to the same public continuation.
        return true;
    }
}
