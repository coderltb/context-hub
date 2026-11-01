namespace ContextHub.Application.Interfaces.Catalog;

/// <summary>
/// Produces optional local search embeddings; absence selects explicit lexical fallback.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Encodes a request-only search query with the pinned query preprocessing.
    /// </summary>
    /// <param name="text">The search text, never persisted or logged.</param>
    /// <param name="cancellationToken">Cancels bounded inference.</param>
    /// <returns>The normalized embedding, or null when the local model is unavailable.</returns>
    Task<EmbeddingResultModel?> EncodeQueryAsync(string text, CancellationToken cancellationToken);

    /// <summary>
    /// Encodes a public package passage using the same pinned model identity.
    /// </summary>
    /// <param name="text">The public package search text.</param>
    /// <param name="cancellationToken">Cancels bounded inference.</param>
    /// <returns>The normalized embedding, or null when the local model is unavailable.</returns>
    Task<EmbeddingResultModel?> EncodePassageAsync(string text, CancellationToken cancellationToken);
}
