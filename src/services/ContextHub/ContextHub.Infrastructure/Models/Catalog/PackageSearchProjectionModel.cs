namespace ContextHub.Infrastructure.Models.Catalog;

/// <summary>
/// Carries a prepared derived search projection bound to the release version observed before inference.
/// </summary>
/// <param name="Id">The immutable release identity.</param>
/// <param name="ConcurrencyVersion">The PostgreSQL version that must still match before writing.</param>
/// <param name="SearchText">The current allowlisted package metadata and positive guidance.</param>
/// <param name="Embedding">The optional normalized vector prepared outside the transaction.</param>
/// <param name="EmbeddingModel">The model identity associated with the vector.</param>
public sealed record PackageSearchProjectionModel(
    Guid Id,
    uint ConcurrencyVersion,
    string SearchText,
    float[]? Embedding,
    string? EmbeddingModel);
