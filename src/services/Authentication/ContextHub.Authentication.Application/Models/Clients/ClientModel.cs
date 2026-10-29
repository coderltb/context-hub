namespace ContextHub.Authentication.Application.Models.Clients;

/// <summary>
/// Carries Application state across the persistence port without exposing an EF entity.
/// </summary>
public sealed record ClientModel
{
    /// <summary>
    /// Gets or initializes the application identifier.
    /// </summary>
    public Guid ClientId { get; init; }

    /// <summary>
    /// Gets or initializes the administrator-supplied display label.
    /// </summary>
    public string DisplayName { get; init; } = "";

    /// <summary>
    /// Gets or initializes the current application state.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Gets or initializes the UTC creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets or initializes the creating administrator.
    /// </summary>
    public Guid CreatedBy { get; init; }

    /// <summary>
    /// Gets or initializes the UTC update timestamp.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets or initializes the updating administrator.
    /// </summary>
    public Guid UpdatedBy { get; init; }

    /// <summary>
    /// Gets or initializes the opaque persistence version.
    /// </summary>
    public string ConcurrencyToken { get; init; } = "";
}
