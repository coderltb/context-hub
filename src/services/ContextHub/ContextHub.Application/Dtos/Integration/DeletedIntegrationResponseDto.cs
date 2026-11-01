namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Confirms a committed integration deletion.
/// </summary>
public sealed class DeletedIntegrationResponseDto
{
    /// <summary>
    /// Gets whether the targeted connection was deleted.
    /// </summary>
    public bool Deleted { get; init; }
}

