namespace ContextHub.Authentication.Application.Constants;

/// <summary>
/// Stores native account and business event templates without raw credentials or serialized payloads.
/// </summary>
public static class AuthenticationLogConstants
{
    /// <summary>
    /// Records the committed security event without credentials or a serialized request.
    /// </summary>
    public const string PASSWORD_CHANGED = "Owner password change committed and human grants were revoked";

    /// <summary>
    /// Identifies a completed security event without logging proof or password.
    /// </summary>
    public const string RECOVERY_COMPLETED = "Owner password recovery committed and human grants were revoked";

    /// <summary>
    /// Records issuance metadata without the raw response or persisted digest.
    /// </summary>
    public const string TOKEN_ISSUED = "Client {ClientId} credential {TokenId} issued with {ToolCount} tool grants";

    /// <summary>
    /// Records the saved client state without its submitted label or concurrency material.
    /// </summary>
    public const string CLIENT_UPDATED = "Client {ClientId} updated, active {IsActive}";

    /// <summary>
    /// Records safe revocation metadata, including the original revocation instant for repeated calls.
    /// </summary>
    public const string TOKEN_REVOKED = "Client {ClientId} credential {TokenId} revoked at {RevokedAt}";

    /// <summary>
    /// Records the classification immediately before an expected Application failure.
    /// </summary>
    public const string OPERATION_REJECTED = "Authentication operation {Operation} rejected with {FailureKind}";

    /// <summary>
    /// Records a successful native account state change without exposing credential material.
    /// </summary>
    public const string OPERATION_COMPLETED = "Authentication operation {Operation} completed";

    /// <summary>
    /// Records a grant outcome without serializing the protocol request or principal.
    /// </summary>
    public const string GRANT_DECIDED = "Authentication grant {Operation} decided {Decision}";

    /// <summary>
    /// Records present-account validation without subject or token contents.
    /// </summary>
    public const string GRANT_VALIDATED = "Authentication native token grant allowed {Allowed}";
}
