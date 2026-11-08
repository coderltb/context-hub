namespace ContextHub.Infrastructure.Models.Mcp;

/// <summary>
/// Projects live authority metadata without loading JSON contracts.
/// </summary>
public sealed record ToolStateModel
{
    /// <summary>
    /// Gets the stable database identity used only by Owner operations.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the fixed capability name.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets current global enablement.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Gets the native xmin value.
    /// </summary>
    public uint ConcurrencyVersion { get; init; }

    /// <summary>
    /// Gets the current display title.
    /// </summary>
    public string Title { get; init; } = "";

    /// <summary>
    /// Gets the current discovery description.
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// Gets the current UTC metadata update time.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets the logical Owner who last updated configuration.
    /// </summary>
    public Guid? UpdatedBy { get; init; }
}
