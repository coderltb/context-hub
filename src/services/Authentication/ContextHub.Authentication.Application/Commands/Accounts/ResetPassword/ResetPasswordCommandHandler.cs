namespace ContextHub.Authentication.Application.Commands.Accounts.ResetPassword;

/// <summary>
/// Coordinates the recovery use case while keeping native credential IO behind the Application-owned port.
/// </summary>
/// <param name="passwordRecoveryService">The native reset and transaction boundary.</param>
/// <param name="authService">The shared accessor for the explicitly authenticated recovery principal.</param>
public sealed class ResetPasswordCommandHandler(
    IPasswordRecoveryService passwordRecoveryService,
    IAuthService authService)
    : IRequestHandler<ResetPasswordCommand, PasswordResetOutcome>
{
    /// <summary>
    /// Applies the form workflow without returning credential diagnostics or creating an Owner session.
    /// </summary>
    /// <param name="request">The typed form input, never a log payload.</param>
    /// <param name="cancellationToken">Cancels downstream operations.</param>
    /// <returns>The safe continuation or outcome.</returns>
    public async ValueTask<PasswordResetOutcome> Handle(
        ResetPasswordCommand request,
        CancellationToken cancellationToken)
    {
        // Native cookies use NameIdentifier; do not fall back to the protocol sub claim or accept hidden identity fields.
        var proof = authService.Get<string>(AuthenticationRecoveryConstants.RESET_TOKEN_CLAIM);
        if (!authService.IsAuthenticated || !authService.TryGet<Guid>(ClaimTypes.NameIdentifier, out var userId) ||
            authService.Get<string>(AuthenticationConstants.ACTOR_TYPE) != AuthenticationRecoveryConstants.RECOVERY_ACTOR ||
            string.IsNullOrEmpty(proof))
        {
            return PasswordResetOutcome.Rejected;
        }

        return await passwordRecoveryService.ResetAsync(new(userId, proof, request.Request.Password), cancellationToken);
    }
}
