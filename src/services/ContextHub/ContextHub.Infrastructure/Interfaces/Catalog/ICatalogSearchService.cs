namespace ContextHub.Infrastructure.Interfaces.Catalog;

/// <summary>
/// Exposes the service-owned catalog search and inference boundary.
/// </summary>
public interface ICatalogSearchService
{
    /// <summary>
    /// Searches latest eligible releases without persisting or logging the query.
    /// </summary>
    /// <param name="request">The bounded query, filters and optional continuation.</param>
    /// <param name="cancellationToken">Cancels inference and the database command.</param>
    /// <returns>The ranked page, its explicit search mode and optional continuation.</returns>
    Task<CatalogSearchResponseDto> SearchAsync(
        CatalogSearchRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Uses the same pinned passage preprocessing for publication as query inference uses for search.
    /// </summary>
    /// <param name="text">Only public package search content.</param>
    /// <param name="cancellationToken">Cancels bounded local inference.</param>
    /// <returns>The optional normalized passage embedding.</returns>
    Task<EmbeddingResultModel?> EncodePassageAsync(
        string text,
        CancellationToken cancellationToken);
}
