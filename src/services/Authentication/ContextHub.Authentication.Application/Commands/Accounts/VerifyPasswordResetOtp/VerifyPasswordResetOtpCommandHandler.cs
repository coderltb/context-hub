namespace ContextHub.Authentication.Application.Commands.Accounts.VerifyPasswordResetOtp;

/// <summary>
/// Coordinates the recovery use case while keeping native credential IO behind the Application-owned port.
/// </summary>
/// <param name="passwordRecoveryService">The native proof verification boundary.</param>
public sealed class VerifyPasswordResetOtpCommandHandler(IPasswordRecoveryService passwordRecoveryService)
    : IRequestHandler<VerifyPasswordResetOtpCommand, PasswordRecoveryGrantModel?>
{
    /// <summary>
    /// Applies the form workflow without returning credential diagnostics or creating an Owner session.
    /// </summary>
    /// <param name="request">The typed form input, never a log payload.</param>
    /// <param name="cancellationToken">Cancels downstream operations.</param>
    /// <returns>The safe continuation or outcome.</returns>
    public async ValueTask<PasswordRecoveryGrantModel?> Handle(
        VerifyPasswordResetOtpCommand request,
        CancellationToken cancellationToken)
    {
        // Infrastructure verifies the native proof; only the HTTP adapter may turn this result into a protected cookie.
        return await passwordRecoveryService.VerifyAsync(request.Request, cancellationToken);
    }
}
