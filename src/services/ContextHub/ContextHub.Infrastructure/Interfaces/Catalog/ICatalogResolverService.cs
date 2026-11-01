namespace ContextHub.Infrastructure.Interfaces.Catalog;

/// <summary>
/// Exposes the service-owned live catalog graph and public projection boundary.
/// </summary>
public interface ICatalogResolverService
{
    /// <summary>
    /// Reads public package metadata after checking its dependency closure.
    /// </summary>
    /// <param name="request">The package and optional exact version.</param>
    /// <param name="cancellationToken">Cancels the authoritative snapshot read.</param>
    /// <returns>The allowlisted package result.</returns>
    Task<JsonObject> GetPackageAsync(
        PackageLookupRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns exact releases in deterministic dependency-before-root order.
    /// </summary>
    /// <param name="request">The selected root package.</param>
    /// <param name="cancellationToken">Cancels the snapshot read.</param>
    /// <returns>The complete closure or a safe failure; results are never truncated.</returns>
    Task<JsonObject> ResolveAsync(
        PackageLookupRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Compares actual client capabilities with every selected exact dependency.
    /// </summary>
    /// <param name="request">The root and declared business capabilities.</param>
    /// <param name="cancellationToken">Cancels the snapshot read.</param>
    /// <returns>A successful compatibility decision, including missing-capability issues.</returns>
    Task<JsonObject> CheckCompatibilityAsync(
        CompatibilityRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Exposes a single exact bundle only after the same closure checks as bundle_resolve.
    /// </summary>
    /// <param name="request">The exact resource identity.</param>
    /// <param name="cancellationToken">Cancels the snapshot read.</param>
    /// <returns>The validated bundle object.</returns>
    Task<JsonObject> GetContentAsync(
        PackageLookupRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the selected release's public capability requirements.
    /// </summary>
    /// <param name="request">The exact resource identity.</param>
    /// <param name="cancellationToken">Cancels the snapshot read.</param>
    /// <returns>Only public identifiers and required capabilities.</returns>
    Task<JsonObject> GetRequirementsAsync(
        PackageLookupRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks current lifecycle without treating a revoked release as missing.
    /// </summary>
    /// <param name="request">The package and selected release.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The live status and replacement metadata.</returns>
    Task<JsonObject> CheckRevocationAsync(
        RevocationRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Rechecks a publication candidate's complete graph within the write transaction.
    /// </summary>
    /// <param name="candidate">The not-yet-published exact release.</param>
    /// <param name="cancellationToken">Cancels dependency reads.</param>
    /// <returns>A task that completes only when all graph invariants hold.</returns>
    Task ValidateCandidateAsync(
        PackageVersion candidate,
        CancellationToken cancellationToken);
}
