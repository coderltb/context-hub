namespace ContextHub.Authentication.Infrastructure.Constants;

/// <summary>
/// Defines safe configuration failures without embedding supplied credential material.
/// </summary>
public static class AuthenticationInfrastructureErrorConstants
{
    /// <summary>
    /// Requires the operator-supplied signing and encryption pair in every environment.
    /// </summary>
    public const string TOKEN_KEYS_REQUIRED = "Authentication:SigningKey and Authentication:EncryptionKey must both be supplied.";

    /// <summary>
    /// Explains the minimum supported RSA signing size using the configured threshold.
    /// </summary>
    public const string SIGNING_KEY_TOO_SMALL = "Authentication:SigningKey requires an RSA private key of at least {0} bits.";
}
