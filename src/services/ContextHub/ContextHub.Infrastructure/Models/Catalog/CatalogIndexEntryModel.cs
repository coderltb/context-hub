namespace ContextHub.Infrastructure.Models.Catalog;

/// <summary>
/// Projects only the metadata required by the lightweight catalog index.
/// </summary>
/// <param name="PackageId">The public package identity.</param>
/// <param name="PackageType">The public package kind.</param>
/// <param name="DisplayName">The English display name.</param>
/// <param name="Summary">The public English summary.</param>
/// <param name="Version">The exact eligible stable release.</param>
public sealed record CatalogIndexEntryModel(string PackageId, string PackageType, string DisplayName, string Summary, string Version);
