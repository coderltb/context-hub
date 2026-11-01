namespace ContextHub.Application.Dtos.Catalog;

/// <summary>
/// Defines the PreviewFinding result contract without persistence entities.
/// </summary>
public sealed record PreviewFindingResponseDto
{
    /// <summary>
    /// Gets the stable safe finding code for automated preview handling.
    /// </summary>
    public string Code { get; init; } = "";

    /// <summary>
    /// Gets the error, warning or info severity; errors block publication.
    /// </summary>
    public string Severity { get; init; } = "";

    /// <summary>
    /// Gets the JSON Pointer of the affected content, without private source values.
    /// </summary>
    public string Path { get; init; } = "";

    /// <summary>
    /// Gets the safe English explanation without raw source or credential values.
    /// </summary>
    public string Message { get; init; } = "";
}
