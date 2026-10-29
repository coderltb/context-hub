namespace ContextHub.Observability.Enums;

/// <summary>
/// Selects one supported console rendering mode without changing the configured Serilog event level.
/// </summary>
public enum ConsoleLogFormat
{
    /// <summary>
    /// Writes one credential-masked compact JSON event to the primary console stream.
    /// </summary>
    CompactJson = 0,

    /// <summary>
    /// Writes one credential-masked human-readable event to the primary console stream.
    /// </summary>
    Text = 1,

    /// <summary>
    /// Writes compact JSON to stdout and an additional readable copy to stderr.
    /// </summary>
    Both = 2
}
