namespace ContextHub.Infrastructure.Models.Catalog;

/// <summary>
/// Rechecks live package state without returning a possibly stale tracked preparation entity.
/// </summary>
/// <param name="IsActive">The current publication enablement.</param>
/// <param name="ConcurrencyVersion">The database version of the metadata used to derive search text.</param>
public sealed record PackagePublicationStateModel(bool IsActive, uint ConcurrencyVersion);
