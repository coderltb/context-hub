namespace ContextHub.Infrastructure.Options.Integration;

/// <summary>
/// Holds Owner-managed GitHub App credentials and authoring-source aliases supplied by deployment.
/// </summary>
public sealed class GitHubOptions
{
    /// <summary>
    /// Gets or sets the GitHub App identifier.
    /// </summary>
    public string AppId { get; set; } = "";

    /// <summary>
    /// Gets or sets the installation permitted to read the approved repositories.
    /// </summary>
    public long InstallationId { get; set; }

    /// <summary>
    /// Gets or sets the PEM private key supplied outside source control.
    /// </summary>
    public string PrivateKey { get; set; } = "";

    /// <summary>
    /// Gets or sets the logical publication alias to persisted repository identifier allowlist.
    /// </summary>
    public Dictionary<string, Guid> AuthoringRepositories { get; set; } = new(StringComparer.Ordinal);
}
