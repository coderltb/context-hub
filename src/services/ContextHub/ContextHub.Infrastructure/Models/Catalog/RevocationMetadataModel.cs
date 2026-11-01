namespace ContextHub.Infrastructure.Models.Catalog;

/// <summary>
/// Projects only the metadata required by the public revocation resource.
/// </summary>
/// <param name="PackageId">The public package identity.</param>
/// <param name="Version">The revoked exact release.</param>
/// <param name="ReplacementVersion">The optional replacement hint.</param>
/// <param name="UpdatedAt">The current UTC lifecycle change time.</param>
public sealed record RevocationMetadataModel(string PackageId, string Version, string? ReplacementVersion, DateTimeOffset UpdatedAt);
