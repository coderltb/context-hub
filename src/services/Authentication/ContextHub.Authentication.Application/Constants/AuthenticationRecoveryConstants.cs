namespace ContextHub.Authentication.Application.Constants;

/// <summary>
/// Defines native recovery boundaries and limits without a custom token or options engine.
/// </summary>
public static class AuthenticationRecoveryConstants
{
    /// <summary>
    /// Partitions same-address send reservations.
    /// </summary>
    public const string COOLDOWN_KEY = "cooldown";

    /// <summary>
    /// Partitions fixed hourly send counters.
    /// </summary>
    public const string SEND_BUDGET_KEY = "send";

    /// <summary>
    /// Partitions fixed verification counters independently of resend.
    /// </summary>
    public const string VERIFY_BUDGET_KEY = "verify";

    /// <summary>
    /// Identifies the approved provisioned-role metadata entry.
    /// </summary>
    public const string ROLES_KEY = "all";

    /// <summary>
    /// Marks a native atomic reservation without storing account or credential data.
    /// </summary>
    public const string RESERVATION_VALUE = "1";

    /// <summary>
    /// Names only the native password-recovery cookie scheme.
    /// </summary>
    public const string RECOVERY_SCHEME = "Authentication.PasswordRecovery";

    /// <summary>
    /// Keeps recovery proof separate from native application and protocol cookies.
    /// </summary>
    public const string RECOVERY_COOKIE = "__Host-context-hub-recovery";

    /// <summary>
    /// Identifies a recovery principal that carries no Owner role or scope.
    /// </summary>
    public const string RECOVERY_ACTOR = "password_recovery";

    /// <summary>
    /// Carries the native proof only inside the encrypted recovery cookie.
    /// </summary>
    public const string RESET_TOKEN_CLAIM = "password_reset_token";

    /// <summary>
    /// Bounds the transition between verified OTP and the reset form; this does not extend OTP validity.
    /// </summary>
    public const int COOKIE_LIFETIME_MINUTES = 5;

    /// <summary>
    /// Prevents repeated email requests from bypassing the same-address cooldown.
    /// </summary>
    public const int SEND_COOLDOWN_SECONDS = 60;

    /// <summary>
    /// Bounds accepted recovery-send reservations per normalized email.
    /// </summary>
    public const int MAX_SENDS_PER_HOUR = 5;

    /// <summary>
    /// Bounds all OTP verification attempts, including requests from other browsers.
    /// </summary>
    public const int MAX_VERIFICATION_ATTEMPTS = 5;

    /// <summary>
    /// Keeps the attempt budget independent of resend and verification outcomes.
    /// </summary>
    public const int VERIFICATION_WINDOW_MINUTES = 15;

    /// <summary>
    /// Validates the native numeric proof shape without stripping leading zeroes.
    /// </summary>
    public const string OTP_PATTERN = "^[0-9]{6}$";

    /// <summary>
    /// Partitions hashed coordination keys without storing raw email in key names.
    /// </summary>
    public const string CACHE_NAMESPACE = "authentication-recovery";

    /// <summary>
    /// Versions recovery coordination keys without introducing a new cache format.
    /// </summary>
    public const int KEY_VERSION = 1;

    /// <summary>
    /// Partitions approved role metadata from security decisions.
    /// </summary>
    public const string ROLES_CACHE_NAMESPACE = "authentication-roles";

    /// <summary>
    /// Identifies the anonymous email-entry form.
    /// </summary>
    public const string FORGOT_PAGE = "/forgot-password";

    /// <summary>
    /// Identifies the anonymous native OTP verification form.
    /// </summary>
    public const string VERIFY_PAGE = "/verify-password-reset-otp";

    /// <summary>
    /// Identifies the form requiring the dedicated recovery scheme.
    /// </summary>
    public const string RESET_PAGE = "/reset-password";

    /// <summary>
    /// Provides a service-owned recovery subject without account information.
    /// </summary>
    public const string EMAIL_SUBJECT = "Reset your password";

    /// <summary>
    /// Formats only a native six-digit proof, never consumer-authored HTML.
    /// </summary>
    public const string EMAIL_TEXT = "Your password reset code is {0}. Enter it on the password recovery page. If you did not request this, ignore this email.";

    /// <summary>
    /// Keeps the HTML alternative separate from the plain-text message.
    /// </summary>
    public const string EMAIL_HTML = "<p>Your password reset code is <strong>{0}</strong>.</p><p>Enter it on the password recovery page. If you did not request this, ignore this email.</p>";

    /// <summary>
    /// Prevents the email request result from disclosing account existence.
    /// </summary>
    public const string GENERIC_REQUEST_MESSAGE = "If the account is eligible, a password reset code has been sent.";

    /// <summary>
    /// Recognizes native password-policy result codes without returning their messages.
    /// </summary>
    public const string NATIVE_PASSWORD_ERROR_PREFIX = "Password";
}
