namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Returns public reference excerpts with the selected library metadata.
/// </summary>
/// <param name="Library">The selected public library and optional version.</param>
/// <param name="Documents">The accepted public source excerpts.</param>
/// <param name="RetrievedAt">The UTC retrieval timestamp.</param>
public sealed record LibraryDocumentationResponseDto(
    LibraryDocumentationLibraryResponseDto Library,
    IReadOnlyList<LibraryDocumentationDocumentResponseDto> Documents,
    DateTimeOffset RetrievedAt);

