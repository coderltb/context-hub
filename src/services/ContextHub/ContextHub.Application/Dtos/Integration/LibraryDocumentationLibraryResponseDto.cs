namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Identifies the public library without exposing its private provider identifier.
/// </summary>
/// <param name="Name">The selected public library name.</param>
/// <param name="Version">The requested advertised version, omitted when unspecified.</param>
public sealed record LibraryDocumentationLibraryResponseDto(
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Version);

