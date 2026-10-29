namespace ContextHub.Authentication.Application.Constants;

/// <summary>
/// Holds internal failure classifications separately from public API error vocabulary.
/// </summary>
public static class AuthenticationErrorConstants
{
    /// <summary>
    /// Classifies a native role-provisioning race without exposing database constraint details.
    /// </summary>
    public const string IDENTITY_PROVISIONING_CONFLICT = "identity_provisioning_conflict";

    /// <summary>
    /// Identifies a rejected current-password proof or native password-policy update without publishing its details.
    /// </summary>
    public const string PASSWORD_CHANGE_REJECTED = "password_change_rejected";

    /// <summary>
    /// Rejects a mutation if native grant revocation could not be persisted inside the same transaction.
    /// </summary>
    public const string GRANT_REVOCATION_FAILED = "Human authorization revocation could not be completed.";

    /// <summary>
    /// Classifies a rejected native reset so the owned transaction rolls back before returning a safe outcome.
    /// </summary>
    public const string RESET_REJECTED = "password_reset_rejected";

    /// <summary>
    /// Distinguishes native password-policy rejection only within the service boundary.
    /// </summary>
    public const string PASSWORD_POLICY_REJECTED = "password_reset_policy_rejected";

    /// <summary>
    /// Does not distinguish unknown, inactive, expired or invalid recovery proof.
    /// </summary>
    public const string INVALID_PROOF_MESSAGE = "The code could not be verified. Request another code and try again.";

    /// <summary>
    /// Describes a safe form failure without copying native credential diagnostics.
    /// </summary>
    public const string PASSWORD_REJECTED_MESSAGE = "The password does not meet the account password requirements.";

    /// <summary>
    /// Identifies a conflicting recovery address without including the submitted email.
    /// </summary>
    public const string ACCOUNT_EMAIL_CONFLICT = "account_email_conflict";

    /// <summary>
    /// Classifies invalid account metadata without echoing the submitted data.
    /// </summary>
    public const string USER_METADATA_INVALID = "user_metadata_invalid";

    /// <summary>
    /// Describes rejected account metadata internally.
    /// </summary>
    public const string INVALID_ACCOUNT_METADATA = "Invalid account metadata.";

    /// <summary>
    /// Classifies invitation issuance denied by current account state.
    /// </summary>
    public const string INVITATION_STATE_INVALID = "invitation_state_invalid";

    /// <summary>
    /// Describes unavailable initial password setup without account details.
    /// </summary>
    public const string SETUP_UNAVAILABLE = "Account setup is unavailable.";

    /// <summary>
    /// Classifies an attempt to remove the final active Owner.
    /// </summary>
    public const string LAST_ACTIVE_OWNER = "last_active_owner";

    /// <summary>
    /// Describes an account invariant conflict internally.
    /// </summary>
    public const string ACCOUNT_POLICY_CONFLICT = "The operation conflicts with account policy.";

    /// <summary>
    /// Describes stale account concurrency state without disclosing its current version.
    /// </summary>
    public const string ACCOUNT_CHANGED = "The account changed.";

    /// <summary>
    /// Classifies an invalid native role selection.
    /// </summary>
    public const string ROLES_INVALID = "roles_invalid";

    /// <summary>
    /// Describes rejected role input without publishing role enumeration data.
    /// </summary>
    public const string INVALID_ROLE_SELECTION = "Invalid role selection.";

    /// <summary>
    /// Classifies an account unavailable to the current operation.
    /// </summary>
    public const string USER_MISSING = "user_missing";

    /// <summary>
    /// Describes an unavailable account without credentials or identifiers.
    /// </summary>
    public const string ACCOUNT_UNAVAILABLE = "Account unavailable.";

    /// <summary>
    /// Describes an internal protocol registration mismatch without client secrets.
    /// </summary>
    public const string PROTOCOL_CLIENT_UNAVAILABLE = "The registered protocol client is unavailable.";

    /// <summary>
    /// Keeps failed interactive sign-in independent of account existence or lockout details.
    /// </summary>
    public const string SIGN_IN_FAILED = "Sign-in could not be completed.";

    /// <summary>
    /// Does not distinguish invalid, expired, consumed or unavailable invitations.
    /// </summary>
    public const string INVITATION_UNAVAILABLE = "Account setup could not be completed.";

    /// <summary>
    /// Reports unusable HMAC configuration without disclosing its value.
    /// </summary>
    public const string INVALID_CLIENT_TOKEN_KEY = "The client-token signing key must contain at least 32 bytes.";

    /// <summary>
    /// Identifies a missing native protocol request without exposing submitted protocol values.
    /// </summary>
    public const string OIDC_REQUEST_UNAVAILABLE = "OIDC request is unavailable.";

    /// <summary>
    /// Identifies an expected failure reported by a native Identity manager.
    /// </summary>
    public const string IDENTITY_OPERATION_REJECTED = "identity_operation_rejected";

    /// <summary>
    /// Provides safe context without copying native validation messages or submitted credentials.
    /// </summary>
    public const string ACCOUNT_OPERATION_REJECTED = "The account operation was rejected.";

    /// <summary>
    /// Identifies invalid public account ordering.
    /// </summary>
    public const string INVALID_SORT = "sort_invalid";

    /// <summary>
    /// Describes an invalid ordering request without echoing its value.
    /// </summary>
    public const string INVALID_SORT_MESSAGE = "Invalid query ordering.";

    /// <summary>
    /// Describes an internal mismatch between the parser's allowlist and the typed selector map.
    /// </summary>
    public const string UNSUPPORTED_PARSED_SORT_FIELD = "The parsed ordering field is unsupported.";

    /// <summary>
    /// Identifies a missing application without disclosing storage details.
    /// </summary>
    public const string CLIENT_MISSING = "client_missing";

    /// <summary>
    /// Identifies issuance denied by application state.
    /// </summary>
    public const string CLIENT_INACTIVE = "client_inactive";

    /// <summary>
    /// Identifies a stale expected version.
    /// </summary>
    public const string CONCURRENCY_CONFLICT = "concurrency_conflict";

    /// <summary>
    /// Identifies a missing credential scoped to its application.
    /// </summary>
    public const string TOKEN_MISSING = "token_missing";

    /// <summary>
    /// Provides safe internal context for an unavailable application.
    /// </summary>
    public const string CLIENT_UNAVAILABLE = "Client unavailable.";

    /// <summary>
    /// Provides safe internal context for an unavailable credential.
    /// </summary>
    public const string TOKEN_UNAVAILABLE = "Credential unavailable.";

    /// <summary>
    /// Provides safe internal context for an optimistic concurrency conflict.
    /// </summary>
    public const string ENTITY_CHANGED = "The entity changed.";
    /// <summary>
    /// Describes invalid explicit credential expiration without publishing secret material.
    /// </summary>
    public const string CLIENT_TOKEN_EXPIRY_INVALID = "The token expiry must be in the future.";
}
