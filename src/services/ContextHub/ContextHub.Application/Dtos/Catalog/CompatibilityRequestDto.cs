namespace ContextHub.Application.Dtos.Catalog;

/// <summary>
/// Defines the Compatibility input contract without persistence entities.
/// </summary>
public sealed record CompatibilityRequestDto
{
    /// <summary>
    /// Gets the stable public package identity, never the internal database UUID.
    /// </summary>
    public string PackageId { get; init; } = "";

    /// <summary>
    /// Gets the exact stable X.Y.Z release; lookup requests may omit it to select the latest published release.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the actual business capabilities declared by the client, independently of authentication grants.
    /// </summary>
    public IReadOnlyList<string> ClientCapabilities { get; init; } = [];
}
