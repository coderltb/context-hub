namespace ContextHub.Application.Queries.Catalog;

/// <summary>
/// Defines the authorized GetPackageVersion catalog operation.
/// </summary>
/// <param name="PackageId">The public package identifier.</param>
/// <param name="Version">The exact stable release.</param>
public sealed record GetPackageVersionQuery(string PackageId, string Version) : IRequest<ApiResponse<VersionResponseDto>>, IAdminRequest;
