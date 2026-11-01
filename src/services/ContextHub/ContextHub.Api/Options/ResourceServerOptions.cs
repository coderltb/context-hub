namespace ContextHub.Api.Options;

/// <summary>
/// Holds this resource server's introspection identity; secrets are supplied by the host configuration providers.
/// </summary>
public sealed class ResourceServerOptions
{
    /// <summary>
    /// Gets or sets the HTTPS issuer that owns token and current-rights validation.
    /// </summary>
    public string Authority { get; set; } = "";

    /// <summary>
    /// Gets or sets this server's confidential introspection application identifier.
    /// </summary>
    public string ClientId { get; set; } = HubConstants.AUDIENCE;

    /// <summary>
    /// Gets or sets the backchannel credential, never a consumer token or signing key.
    /// </summary>
    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// Gets or sets exact browser origins permitted to call this host.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];
}
