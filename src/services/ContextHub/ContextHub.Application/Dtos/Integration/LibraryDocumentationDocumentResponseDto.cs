namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Contains a bounded public documentation excerpt and its source.
/// </summary>
/// <param name="Title">The public section title.</param>
/// <param name="Url">The HTTPS source without credentials or query parameters.</param>
/// <param name="Excerpt">The bounded reference excerpt.</param>
public sealed record LibraryDocumentationDocumentResponseDto(
    string Title,
    string Url,
    string Excerpt);

