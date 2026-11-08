namespace ContextHub.Application.Interfaces.Catalog;

/// <summary>
/// Provides catalog operations through approved contracts while Infrastructure owns persistence and source I/O.
/// </summary>
public interface ICatalogService
{
    /// <summary>
    /// Lists editable package metadata with administrative paging and filters.
    /// </summary>
    /// <param name="request">The operation-specific input validated by the Application pipeline.</param>
    /// <param name="cancellationToken">Cancels the owned I/O and transaction work.</param>
    /// <returns>The current contract projection; unavailable or disallowed content remains an explicit failure.</returns>
    Task<PageResponse<PackageResponseDto>> GetPackagesAsync(
        PackageListRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current Owner-visible identity and concurrency metadata.
    /// </summary>
    /// <param name="packageId">The canonical public package identity; internal UUIDs are never exposed.</param>
    /// <param name="cancellationToken">Cancels the owned I/O and transaction work.</param>
    /// <returns>The current contract projection; unavailable or disallowed content remains an explicit failure.</returns>
    Task<PackageResponseDto> GetPackageDetailAsync(Guid packageId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a stable package identity without publishing content.
    /// </summary>
    /// <param name="request">The operation-specific input validated by the Application pipeline.</param>
    /// <param name="cancellationToken">Cancels the owned I/O and transaction work.</param>
    /// <returns>The current contract projection; unavailable or disallowed content remains an explicit failure.</returns>
    Task<PackageResponseDto> CreatePackageAsync(
        CreatePackageRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates only editable metadata while preserving stable identity.
    /// </summary>
    /// <param name="packageId">The canonical public package identity; internal UUIDs are never exposed.</param>
    /// <param name="request">The operation-specific input validated by the Application pipeline.</param>
    /// <param name="cancellationToken">Cancels the owned I/O and transaction work.</param>
    /// <returns>The current contract projection; unavailable or disallowed content remains an explicit failure.</returns>
    Task<PackageResponseDto> UpdatePackageAsync(
        Guid packageId,
        UpdatePackageRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists release metadata without loading source content into each response row.
    /// </summary>
    /// <param name="packageId">The canonical public package identity; internal UUIDs are never exposed.</param>
    /// <param name="request">The operation-specific input validated by the Application pipeline.</param>
    /// <param name="cancellationToken">Cancels the owned I/O and transaction work.</param>
    /// <returns>The current contract projection; unavailable or disallowed content remains an explicit failure.</returns>
    Task<PageResponse<VersionSummaryResponseDto>> GetVersionsAsync(
        Guid packageId,
        VersionListRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets exact Owner-only immutable content and live lifecycle metadata.
    /// </summary>
    /// <param name="packageId">The canonical public package identity; internal UUIDs are never exposed.</param>
    /// <param name="version">The exact canonical stable release, without ranges or build metadata.</param>
    /// <param name="cancellationToken">Cancels the owned I/O and transaction work.</param>
    /// <returns>The current contract projection; unavailable or disallowed content remains an explicit failure.</returns>
    Task<VersionResponseDto> GetVersionAsync(
        Guid packageId,
        string version,
        CancellationToken cancellationToken);

    /// <summary>
    /// Evaluates exact source content without staging or saving database records.
    /// </summary>
    /// <param name="packageId">The canonical public package identity; internal UUIDs are never exposed.</param>
    /// <param name="request">The operation-specific input validated by the Application pipeline.</param>
    /// <param name="cancellationToken">Cancels the owned I/O and transaction work.</param>
    /// <returns>The current contract projection; unavailable or disallowed content remains an explicit failure.</returns>
    Task<PackagePreviewResponseDto> PreviewAsync(
        Guid packageId,
        PublishVersionRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Publishes reviewed immutable content after transaction-time dependency checks.
    /// </summary>
    /// <param name="packageId">The canonical public package identity; internal UUIDs are never exposed.</param>
    /// <param name="request">The operation-specific input validated by the Application pipeline.</param>
    /// <param name="cancellationToken">Cancels the owned I/O and transaction work.</param>
    /// <returns>The current contract projection; unavailable or disallowed content remains an explicit failure.</returns>
    Task<VersionResponseDto> PublishAsync(
        Guid packageId,
        PublishVersionRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Marks a release deprecated without changing its immutable content.
    /// </summary>
    /// <param name="packageId">The canonical public package identity; internal UUIDs are never exposed.</param>
    /// <param name="version">The exact canonical stable release, without ranges or build metadata.</param>
    /// <param name="request">The operation-specific input validated by the Application pipeline.</param>
    /// <param name="cancellationToken">Cancels the owned I/O and transaction work.</param>
    /// <returns>The current contract projection; unavailable or disallowed content remains an explicit failure.</returns>
    Task<VersionResponseDto> DeprecateAsync(
        Guid packageId,
        string version,
        LifecycleVersionRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Permanently revokes a release while retaining publication evidence.
    /// </summary>
    /// <param name="packageId">The canonical public package identity; internal UUIDs are never exposed.</param>
    /// <param name="version">The exact canonical stable release, without ranges or build metadata.</param>
    /// <param name="request">The operation-specific input validated by the Application pipeline.</param>
    /// <param name="cancellationToken">Cancels the owned I/O and transaction work.</param>
    /// <returns>The current contract projection; unavailable or disallowed content remains an explicit failure.</returns>
    Task<VersionResponseDto> RevokeAsync(
        Guid packageId,
        string version,
        LifecycleVersionRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Ranks latest eligible releases using the fixed PostgreSQL search contract.
    /// </summary>
    /// <param name="request">The operation-specific input validated by the Application pipeline.</param>
    /// <param name="cancellationToken">Cancels the owned I/O and transaction work.</param>
    /// <returns>The current contract projection; unavailable or disallowed content remains an explicit failure.</returns>
    Task<CatalogSearchResultModel> SearchAsync(
        CatalogSearchRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets only allowlisted consumer metadata after lifecycle and closure checks.
    /// </summary>
    /// <param name="request">The operation-specific input validated by the Application pipeline.</param>
    /// <param name="cancellationToken">Cancels the owned I/O and transaction work.</param>
    /// <returns>The current contract projection; unavailable or disallowed content remains an explicit failure.</returns>
    Task<JsonObject> GetPublicPackageAsync(
        PackageLookupRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns a complete exact dependency closure in deterministic dependency order.
    /// </summary>
    /// <param name="request">The operation-specific input validated by the Application pipeline.</param>
    /// <param name="cancellationToken">Cancels the owned I/O and transaction work.</param>
    /// <returns>The current contract projection; unavailable or disallowed content remains an explicit failure.</returns>
    Task<JsonObject> ResolveAsync(PackageLookupRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Compares actual client capabilities with the resolved closure requirements.
    /// </summary>
    /// <param name="request">The operation-specific input validated by the Application pipeline.</param>
    /// <param name="cancellationToken">Cancels the owned I/O and transaction work.</param>
    /// <returns>The current contract projection; unavailable or disallowed content remains an explicit failure.</returns>
    Task<JsonObject> CheckCompatibilityAsync(
        CompatibilityRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks live revocation metadata for one selected release.
    /// </summary>
    /// <param name="request">The operation-specific input validated by the Application pipeline.</param>
    /// <param name="cancellationToken">Cancels the owned I/O and transaction work.</param>
    /// <returns>The current contract projection; unavailable or disallowed content remains an explicit failure.</returns>
    Task<JsonObject> GetRevocationsAsync(RevocationRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Lists lightweight active-package metadata for public discovery.
    /// </summary>
    /// <param name="cancellationToken">Cancels the owned I/O and transaction work.</param>
    /// <returns>The current contract projection; unavailable or disallowed content remains an explicit failure.</returns>
    Task<JsonNode> GetCatalogIndexAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns only the selected bundle after live closure and lifecycle checks.
    /// </summary>
    /// <param name="request">The exact resource identity.</param>
    /// <param name="cancellationToken">Cancels the authoritative read.</param>
    /// <returns>The validated public bundle.</returns>
    Task<JsonObject> GetPublicContentAsync(
        PackageLookupRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns public requirements for the selected exact resource.
    /// </summary>
    /// <param name="request">The exact package resource identity.</param>
    /// <param name="cancellationToken">Cancels the authoritative read.</param>
    /// <returns>The public capability requirements.</returns>
    Task<JsonObject> GetRequirementsAsync(
        PackageLookupRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lists only public metadata for revoked package releases.
    /// </summary>
    /// <param name="cancellationToken">Cancels the authoritative read.</param>
    /// <returns>The current public revocation index.</returns>
    Task<JsonNode> GetRevocationsIndexAsync(CancellationToken cancellationToken);
}
