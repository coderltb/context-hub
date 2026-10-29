namespace ContextHub.Shared.Dtos.Options.Email;

/// <summary>
/// Binds only transactional-email settings; provider credentials are supplied outside source and images.
/// </summary>
public sealed class SendGridOptions
{
    /// <summary>
    /// Gets or sets the external Mail Send credential, which must never enter diagnostics.
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// Gets or sets the provider-verified sender address.
    /// </summary>
    public string FromEmail { get; set; } = "";

    /// <summary>
    /// Gets or sets the sender display name.
    /// </summary>
    public string FromName { get; set; } = "";

    /// <summary>
    /// Gets or sets the complete HTTP send timeout, including response buffering after resilience handlers return.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(SharedConstants.EmailVariables.REQUEST_TIMEOUT_SECONDS);
}
