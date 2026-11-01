namespace ContextHub.Infrastructure.Models.Catalog;

/// <summary>
/// Reads immutable database content only on a cache miss; private provenance stays inside this boundary.
/// </summary>
/// <param name="Manifest">The internal immutable manifest JSON.</param>
/// <param name="Bundle">The validated immutable bundle JSON.</param>
public sealed record ImmutablePackageSourceModel(string Manifest, string Bundle);
