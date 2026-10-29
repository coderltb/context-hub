namespace ContextHub.Core.Constants;

/// <summary>
/// Stores stable Core identifiers separately from error messages and structured log templates.
/// </summary>
public static class CoreConstants
{
    /// <summary>
    /// Defines the selected mail transport and its native resilience defaults.
    /// </summary>
    public static class EmailVariables
    {
        /// <summary>
        /// Names the dedicated factory transport; its circuit is independent from REST clients.
        /// </summary>
        public const string HTTP_CLIENT_NAME = "SendGrid";

        /// <summary>
        /// Selects native HTTP resilience options under the host email section.
        /// </summary>
        public const string RESILIENCE_SECTION = "Resilience";

        /// <summary>
        /// Requires enough observations before opening the mail circuit.
        /// </summary>
        public const int MINIMUM_THROUGHPUT = 5;

        /// <summary>
        /// Opens the circuit when at least half of the observed attempts fail.
        /// </summary>
        public const double FAILURE_RATIO = 0.5;

        /// <summary>
        /// Bounds the circuit observation window in seconds.
        /// </summary>
        public const int SAMPLING_SECONDS = 30;

        /// <summary>
        /// Pauses mail attempts before allowing a native recovery probe.
        /// </summary>
        public const int BREAK_SECONDS = 15;
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
        /// Allows retries only for safe methods.
        /// </summary>
        public const int MAX_RETRY_ATTEMPTS = 3;

        /// <summary>
        /// Bounds one transport attempt.
        /// </summary>
        public const int ATTEMPT_TIMEOUT_SECONDS = 10;

        /// <summary>
        /// Bounds the complete resilience pipeline.
        /// </summary>
        public const int TOTAL_TIMEOUT_SECONDS = 30;

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
