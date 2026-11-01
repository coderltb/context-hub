namespace ContextHub.Application.Dtos.Catalog;

/// <summary>
/// Defines the PackagePreview result contract without persistence entities.
/// </summary>
public sealed record PackagePreviewResponseDto
{
    /// <summary>
    /// Gets the candidate applicability, or null when content cannot be read safely.
    /// </summary>
    public PackagePreviewPurposeResponseDto? Purpose { get; init; }

    /// <summary>
    /// Gets the factual differences from the nearest lower stable release.
    /// </summary>
    public PackagePreviewChangesResponseDto Changes { get; init; } = new();

    /// <summary>
    /// Gets the changed guidance areas without predicting business outcomes.
    /// </summary>
    public PackagePreviewImpactResponseDto ConsumerImpact { get; init; } = new();

    /// <summary>
    /// Gets the publication decision and safe validation findings.
    /// </summary>
    public PackagePreviewReadinessResponseDto Readiness { get; init; } = new();

    /// <summary>
    /// Gets the allowlisted consumer metadata and guidance without source provenance.
    /// </summary>
    public PackagePublicPreviewResponseDto? PublicPreview { get; init; }
}
