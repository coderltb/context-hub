namespace ContextHub.Infrastructure.Repositories.Catalog;

/// <summary>
/// Reuses shared staging while loading the owner required for exact public release projections.
/// </summary>
/// <param name="contextHubDbContext">The service-owned scoped context.</param>
/// <param name="logger">The specific repository metadata logger.</param>
public sealed class PackageVersionRepository(
    ContextHubDbContext contextHubDbContext,
    ILogger<PackageVersionRepository> logger)
    : GenericRepository<PackageVersion>(contextHubDbContext, logger), IPackageVersionRepository
{
    /// <summary>
    /// Reads current release and parent state together for consumer selection.
    /// </summary>
    /// <param name="packageId">The public package identity.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>Untracked candidate releases with parent state.</returns>
    public async Task<IReadOnlyList<PackageVersion>> GetCandidatesAsync(
        string packageId,
        CancellationToken cancellationToken) =>
        await contextHubDbContext.Versions.AsNoTracking().Include(value => value.Package)
            .Where(value => value.Package.Key == packageId).ToListAsync(cancellationToken);

    /// <summary>
    /// Loads the exact entity for a concurrency-controlled Owner update.
    /// </summary>
    /// <param name="packageId">The public package identity.</param>
    /// <param name="version">The exact stable release.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The tracked release or null.</returns>
    public Task<PackageVersion?> GetTrackedAsync(
        string packageId,
        string version,
        CancellationToken cancellationToken) =>
        contextHubDbContext.Versions.Include(value => value.Package)
            .FirstOrDefaultAsync(
                value => value.Package.Key == packageId && value.Number == version,
                cancellationToken);

    /// <summary>
    /// Releases a failed publication candidate without discarding unrelated staged entities.
    /// </summary>
    /// <param name="candidate">The insert rejected by the unique release constraint.</param>
    public void DetachFailedInsert(PackageVersion candidate) =>
        contextHubDbContext.Entry(candidate).State = EntityState.Detached;

    /// <summary>
    /// Reads only current selection state before immutable content can be served from cache.
    /// </summary>
    /// <param name="packageId">The public package identity.</param>
    /// <param name="cancellationToken">Cancels the authority read.</param>
    /// <returns>Untracked release and parent metadata without JSON payloads.</returns>
    public async Task<IReadOnlyList<PackageVersion>> GetSelectionCandidatesAsync(
        string packageId,
        CancellationToken cancellationToken) =>
        await contextHubDbContext.Versions.AsNoTracking()
            .Where(value => value.Package.Key == packageId)
            .Select(value => new PackageVersion
            {
                Id = value.Id,
                PackageId = value.PackageId,
                Number = value.Number,
                ConcurrencyVersion = value.ConcurrencyVersion,
                Status = value.Status,
                ContentHash = value.ContentHash,
                ReplacementVersion = value.ReplacementVersion,
                PublishedAt = value.PublishedAt,
                Package = new Package
                {
                    Id = value.Package.Id,
                    Key = value.Package.Key,
                    Type = value.Package.Type,
                    Name = value.Package.Name,
                    Summary = value.Package.Summary,
                    IsActive = value.Package.IsActive
                },
                Manifest = "",
                Bundle = ""
            }).ToArrayAsync(cancellationToken);

    /// <summary>
    /// Reads the immutable bytes only after an authoritative selection and a cache miss.
    /// </summary>
    /// <param name="versionId">The selected internal release.</param>
    /// <param name="contentHash">Its expected immutable fingerprint.</param>
    /// <param name="cancellationToken">Cancels the database read.</param>
    /// <returns>The exact stored source content, or null.</returns>
    public Task<ImmutablePackageSourceModel?> GetImmutableContentAsync(
        Guid versionId,
        string contentHash,
        CancellationToken cancellationToken) =>
        FirstOrDefaultAsync<ImmutablePackageSourceModel>(
            predicate: value => value.Id == versionId && value.ContentHash == contentHash,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Stages prepared text/vector values while preserving immutable content and concurrent lifecycle changes.
    /// </summary>
    /// <param name="projection">The prepared projection with its original PostgreSQL version.</param>
    /// <param name="cancellationToken">Cancels the tracked release read.</param>
    /// <returns>The staging operation; saving remains owned by the enclosing Unit of Work.</returns>
    public async Task StageSearchProjectionAsync(
        PackageSearchProjectionModel projection,
        CancellationToken cancellationToken)
    {
        var release = await contextHubDbContext.Versions.FirstAsync(value => value.Id == projection.Id, cancellationToken);

        // Reject a stale vector update when metadata changed during inference.
        contextHubDbContext.Entry(release).Property(value => value.ConcurrencyVersion).OriginalValue = projection.ConcurrencyVersion;
        release.SearchText = projection.SearchText;
        release.Embedding = projection.Embedding;
        release.EmbeddingModel = projection.EmbeddingModel;
    }
}
