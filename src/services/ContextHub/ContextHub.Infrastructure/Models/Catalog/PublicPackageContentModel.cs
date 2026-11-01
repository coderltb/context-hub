namespace ContextHub.Infrastructure.Models.Catalog;

/// <summary>
/// Caches only immutable public content; lifecycle, parent visibility and permission remain database authority.
/// </summary>
/// <param name="Manifest">The allowlisted public manifest JSON.</param>
/// <param name="Bundle">The validated public bundle JSON.</param>
public sealed record PublicPackageContentModel(string Manifest, string Bundle);
