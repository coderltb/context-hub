namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Returns Owner-only canonical configuration and derived discovery status.
/// </summary>
public sealed record ExternalMcpResponseDto
{
    /// <summary>
    /// Gets the Owner-only connection name.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets canonical saved configuration.
    /// </summary>
    public ExternalMcpConfigDto Config { get; init; } = new();

    /// <summary>
    /// Gets rebuildable discovery metadata.
    /// </summary>
    public ExternalMcpDiscoveryDto Discovery { get; init; } = new();

    /// <summary>
    /// Gets the original UTC creation time.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.MinValue;

    /// <summary>
    /// Gets the creating Owner identifier.
    /// </summary>
    public Guid? CreatedBy { get; init; } = null;

    /// <summary>
    /// Gets the latest UTC update time.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.MinValue;

    /// <summary>
    /// Gets the latest Owner identifier.
    /// </summary>
    public Guid? UpdatedBy { get; init; } = null;

    /// <summary>
    /// Gets the opaque PostgreSQL version.
    /// </summary>
    public string ConcurrencyToken { get; init; } = "";

}
