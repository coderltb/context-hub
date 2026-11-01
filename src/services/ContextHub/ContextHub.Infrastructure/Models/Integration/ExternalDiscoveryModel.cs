namespace ContextHub.Infrastructure.Models.Integration;

/// <summary>
/// Stores rebuildable metadata without making cache a permission authority.
/// </summary>
/// <param name="Tools">The bounded private definitions.</param>
/// <param name="RefreshedAt">The successful discovery time.</param>
/// <param name="ConfigHash">The source configuration fingerprint.</param>
public sealed record ExternalDiscoveryModel(IReadOnlyList<ExternalToolModel> Tools, DateTimeOffset RefreshedAt, string ConfigHash);
