namespace ContextHub.Application.Models.Catalog;

/// <summary>
/// Selects two package files from an allowlisted source at one immutable Git commit.
/// </summary>
/// <param name="PackageId">The public package identity expected in the source manifest.</param>
/// <param name="AuthoringRepository">The configured logical source identifier, never an arbitrary remote URL.</param>
/// <param name="SourceCommitSha">The full Git commit approved for publication.</param>
/// <param name="SourcePath">The relative directory containing manifest.json and bundle.json.</param>
public sealed record PackageSourceRequestModel(string PackageId, string AuthoringRepository, string SourceCommitSha, string SourcePath);
