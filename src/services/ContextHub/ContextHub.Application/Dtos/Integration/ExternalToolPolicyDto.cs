namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Selects all reviewed tools or an explicit upstream allowlist.
/// </summary>
public sealed class ExternalToolPolicyDto
{
    /// <summary>
    /// Gets all or allowlist.
    /// </summary>
    public string Mode { get; init; } = "all";

    /// <summary>
    /// Gets exact upstream tool names for allowlist mode only.
    /// </summary>
    public IReadOnlyList<string>? Tools { get; init; }

}

