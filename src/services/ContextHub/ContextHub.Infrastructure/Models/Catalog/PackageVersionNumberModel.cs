namespace ContextHub.Infrastructure.Models.Catalog;

/// <summary>
/// Projects only the metadata required by stable release selection.
/// </summary>
/// <param name="PackageId">The public package identity.</param>
/// <param name="Number">The exact release number.</param>
public sealed record PackageVersionNumberModel(string PackageId, string Number);
