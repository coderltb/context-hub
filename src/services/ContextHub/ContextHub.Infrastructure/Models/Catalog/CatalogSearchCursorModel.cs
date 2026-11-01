namespace ContextHub.Infrastructure.Models.Catalog;

/// <summary>
/// Binds an encrypted cursor to the search shape without storing the private query.
/// </summary>
/// <param name="Offset">The bounded next database offset.</param>
/// <param name="Fingerprint">The normalized query/filter/limit fingerprint.</param>
/// <param name="Mode">The ranking mode fixed for this continuation.</param>
/// <param name="Model">The exact embedding model identity, if semantic search was selected.</param>
/// <param name="Ranking">The fixed SQL ranking contract.</param>
public sealed record CatalogSearchCursorModel(int Offset, string Fingerprint, string Mode, string? Model, string Ranking);
