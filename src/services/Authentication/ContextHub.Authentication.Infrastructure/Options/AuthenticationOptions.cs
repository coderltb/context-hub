namespace ContextHub.Authentication.Infrastructure.Options;

/// <summary>
/// Holds deployment settings; secret values are provided outside committed appsettings.
/// </summary>
public sealed class AuthenticationOptions
{
    // Native binding appends to initialized arrays. Keep endpoint defaults empty so deployments receive only their own allowlists.

    /// <summary>
    /// Gets or sets the externally visible HTTPS issuer; production supplies its deployed address.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the deployed Owner Console URL linked from the SSO home page.
    /// </summary>
    public string ConsoleUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Base64-encoded external HMAC key used for client credentials.
    /// </summary>
    public string ClientTokenPepper { get; set; } = "";

    /// <summary>
    /// Gets or sets the backchannel service secret, which is never issued to browser or MCP clients.
    /// </summary>
    public string IntrospectionClientSecret { get; set; } = "";

    /// <summary>
    /// Gets or sets exact callback allowlists supplied by the active environment configuration.
    /// </summary>
    public string[] RedirectUris { get; set; } = [];

    /// <summary>
    /// Gets or sets exact browser destinations permitted after logout.
    /// </summary>
    public string[] PostLogoutRedirectUris { get; set; } = [];

    /// <summary>
    /// Gets or sets the explicit browser origins permitted by the host.
    /// </summary>
    public string[] CorsOrigins { get; set; } = [];

    /// <summary>
    /// Gets or sets the RSA private signing key in PEM format, supplied through environment or container secrets.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the separate 256-bit token encryption key encoded as Base64, supplied outside source.
    /// </summary>
    public string EncryptionKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the positive lifetime used by native refresh-token rotation.
    /// </summary>
    public int RefreshTokenLifetimeHours { get; set; } = 8;

    /// <summary>
    /// Gets or sets trusted immediate proxy addresses; deployment supplies real addresses outside source defaults.
    /// </summary>
    public string[] KnownProxies { get; set; } = [];
}
