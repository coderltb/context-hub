namespace ContextHub.Infrastructure.Interfaces.Catalog;

/// <summary>
/// Provides exact and eligible version reads behind the catalog persistence boundary.
/// </summary>
public interface IPackageVersionRepository : IGenericRepository<PackageVersion>
{
    /// <summary>
    /// Gets release candidates and their owning metadata without tracking consumer reads.
    /// </summary>
    /// <param name="packageId">The public package identity.</param>
    /// <param name="cancellationToken">Cancels the authoritative read.</param>
    /// <returns>All releases for deterministic stable selection.</returns>
    Task<IReadOnlyList<PackageVersion>> GetCandidatesAsync(string packageId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets an exact tracked version for an Owner lifecycle mutation.
    /// </summary>
    /// <param name="packageId">The public package identity.</param>
    /// <param name="version">The exact stable release.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The tracked release or null.</returns>
    Task<PackageVersion?> GetTrackedAsync(string packageId, string version, CancellationToken cancellationToken);

    /// <summary>
    /// Detaches only an insert known to have failed after its owned transaction rolled back.
    /// </summary>
    /// <param name="candidate">The failed publication candidate.</param>
    void DetachFailedInsert(PackageVersion candidate);

    /// <summary>
    /// Reads live lifecycle and parent visibility without selecting immutable JSON content.
    /// </summary>
    /// <param name="packageId">The public package identity.</param>
    /// <param name="cancellationToken">Cancels the database read.</param>
    /// <returns>Untracked release selection metadata.</returns>
    Task<IReadOnlyList<PackageVersion>> GetSelectionCandidatesAsync(string packageId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads immutable content only when its expected content fingerprint still matches.
    /// </summary>
    /// <param name="versionId">The internal selected release identifier.</param>
    /// <param name="contentHash">The fingerprint observed in the live selection read.</param>
    /// <param name="cancellationToken">Cancels the content read.</param>
    /// <returns>The exact immutable source projection, or null.</returns>
    Task<ImmutablePackageSourceModel?> GetImmutableContentAsync(Guid versionId, string contentHash, CancellationToken cancellationToken);

    /// <summary>
    /// Stages only derived search fields against the release version observed during preparation.
    /// </summary>
    /// <param name="projection">The precomputed text/vector and expected PostgreSQL version.</param>
    /// <param name="cancellationToken">Cancels the tracked read; the caller owns save and transaction.</param>
    /// <returns>The completed staging operation without changes to immutable or lifecycle fields.</returns>
    Task StageSearchProjectionAsync(PackageSearchProjectionModel projection, CancellationToken cancellationToken);
}
