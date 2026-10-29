namespace ContextHub.Authentication.Application.Enums;

/// <summary>
/// Describes safe reset outcomes without exposing Identity validation messages or protocol entities.
/// </summary>
public enum PasswordResetOutcome
{
    /// <summary>
    /// Denies reset by default when recovery authority is missing or no longer valid.
    /// </summary>
    Rejected = 0,

    /// <summary>
    /// Keeps the verified form available when the replacement password fails native policy.
    /// </summary>
    PasswordRejected = 1,

    /// <summary>
    /// Confirms that password change and human-grant revocation committed together.
    /// </summary>
    Succeeded = 2
}
