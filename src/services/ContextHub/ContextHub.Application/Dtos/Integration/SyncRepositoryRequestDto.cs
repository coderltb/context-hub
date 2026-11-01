namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Binds explicit sync to the latest repository metadata.
/// </summary>
public sealed class SyncRepositoryRequestDto
{
    /// <summary>
    /// Gets the opaque expected PostgreSQL version.
    /// </summary>
    public string ConcurrencyToken { get; init; } = "";
}
