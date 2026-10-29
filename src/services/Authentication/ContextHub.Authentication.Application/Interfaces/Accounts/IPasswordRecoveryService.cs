namespace ContextHub.Authentication.Application.Interfaces.Accounts;

/// <summary>
/// Exposes native Identity and coordination operations without leaking entities or Redis/provider clients to Application.
/// </summary>
public interface IPasswordRecoveryService
{
    /// <summary>
    /// Reserves the fixed same-address send budget before account lookup.
    /// </summary>
    /// <param name="request">The submitted recovery address.</param>
    /// <param name="cancellationToken">Cancels coordination commands.</param>
    /// <returns>Whether Application may attempt the send workflow.</returns>
    Task<bool> ReserveRequestAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a currently eligible active Owner with an existing password.
    /// </summary>
    /// <param name="emailAddress">The submitted address, normalized by native Identity.</param>
    /// <param name="cancellationToken">Cancels the repository query.</param>
    /// <returns>The approved mail destination, or null without disclosing account details.</returns>
    Task<PasswordRecoveryAccountModel?> GetAccountAsync(string emailAddress, CancellationToken cancellationToken);

    /// <summary>
    /// Generates and sends native numeric reset proof for the approved account.
    /// </summary>
    /// <param name="account">The approved account metadata.</param>
    /// <param name="cancellationToken">Cancels provider IO.</param>
    /// <returns>Whether the provider accepted the email.</returns>
    Task<bool> SendOtpAsync(PasswordRecoveryAccountModel account, CancellationToken cancellationToken);

    /// <summary>
    /// Counts this attempt and verifies the native proof without consuming it or issuing Owner authority.
    /// </summary>
    /// <param name="request">The address and numeric proof.</param>
    /// <param name="cancellationToken">Cancels coordination and account queries.</param>
    /// <returns>Verified metadata for the protected cookie, or null.</returns>
    Task<PasswordRecoveryGrantModel?> VerifyAsync(VerifyPasswordResetOtpRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Revalidates native proof and commits the new password with Owner-grant revocation.
    /// </summary>
    /// <param name="request">The authenticated proof and new password, never caller-selected identity metadata.</param>
    /// <param name="cancellationToken">Cancels the owned database transaction.</param>
    /// <returns>The safe form outcome after commit or rollback.</returns>
    Task<PasswordResetOutcome> ResetAsync(PasswordResetRequestModel request, CancellationToken cancellationToken);
}
