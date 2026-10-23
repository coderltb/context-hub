namespace ContextHub.Core.Constants;

/// <summary>
/// Stores stable Core identifiers separately from error messages and structured log templates.
/// </summary>
public static class CoreConstants
{
    /// <summary>
    /// Bounds pure identifier generation independently of service uniqueness policy.
    /// </summary>
    public static class CodeVariables
    {
        /// <summary>
        /// Maximum number of cryptographically generated decimal digits.
        /// </summary>
        public const int MAX_LENGTH = 32;

        /// <summary>
        /// Default separator between a caller-owned prefix and numeric suffix.
        /// </summary>
        public const string SEPARATOR = "_";
    }

    /// <summary>
    /// Normalized claim defaults, not issuer-specific token validation rules.
    /// </summary>
    public static class AuthenticationVariables
    {
        /// <summary>
        /// Default OpenID Connect subject claim.
        /// </summary>
        public const string SUBJECT_CLAIM = "sub";

        /// <summary>
        /// Unmapped email claim, used only on authenticated identities.
        /// </summary>
        public const string EMAIL_CLAIM = "email";

        /// <summary>
        /// Whitespace-separated scope claim.
        /// </summary>
        public const string SCOPE_CLAIM = "scope";
    }

    /// <summary>
    /// Defines reusable database execution defaults, not service-owned SQL.
    /// </summary>
    public static class DatabaseVariables
    {
        /// <summary>
        /// Limits a default command to thirty seconds.
        /// </summary>
        public const int COMMAND_TIMEOUT_SECONDS = 30;
    }

    /// <summary>
    /// Defines shared JSON safety limits independently of diagnostic payloads.
    /// </summary>
    public static class JsonVariables
    {
        /// <summary>
        /// Bounds nested external JSON to prevent unbounded recursive deserialization.
        /// </summary>
        public const int MAX_DEPTH = 64;
    }

    /// <summary>
    /// Defines the default bound only for convenience reads; raw response streaming remains caller-owned.
    /// </summary>
    public static class HttpVariables
    {
        /// <summary>
        /// Caps facade response consumption at one mebibyte; raw transport reads remain caller-owned.
        /// </summary>
        public const long MAX_RESPONSE_BYTES = 1_048_576;

        /// <summary>
        /// Bounds facade body consumption independently of the HTTP send timeout.
        /// </summary>
        public const int BODY_READ_TIMEOUT_SECONDS = 30;
    }

    /// <summary>
    /// Defines entropy size independently of any service-specific credential format.
    /// </summary>
    public static class TokenVariables
    {
        /// <summary>
        /// Provides two hundred fifty-six bits of cryptographic randomness.
        /// </summary>
        public const int OPAQUE_TOKEN_BYTES = 32;
    }

    /// <summary>
    /// Defines expiration arithmetic defaults independently of token generation and cache policy.
    /// </summary>
    public static class TimeVariables
    {
        /// <summary>
        /// Moves refresh decisions one minute before the real expiration boundary.
        /// </summary>
        public const int EXPIRATION_PADDING_SECONDS = 60;
    }

    /// <summary>
    /// Identifies arguments added to reusable validation message formatters.
    /// </summary>
    public static class ValidationVariables
    {
        /// <summary>
        /// Names the inclusive maximum count placeholder without including submitted collection values.
        /// </summary>
        public const string MAX_COUNT_ARGUMENT = "MaxCount";
    }
}
