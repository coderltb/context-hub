namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Confirms a committed integration deletion.
/// </summary>
public sealed class DeletedIntegrationResponseDto
{
    /// <summary>
    /// Gets the stable Owner connection identity.
    /// </summary>
    public Guid ConnectionId { get; init; }

    /// <summary>
    /// Gets retained lifecycle independently of the enabled configuration.
    /// </summary>
    public string Status { get; init; } = IntegrationConstants.ACTIVE;

    /// <summary>
    /// Gets whether the targeted connection was deleted.
    /// </summary>
    public bool Deleted { get; init; }
}

