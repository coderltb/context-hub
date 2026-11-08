namespace ContextHub.Application.Queries.Catalog;

/// <summary>
/// Defines the authorized GetPackageVersion catalog operation.
/// </summary>
/// <param name="PackageId">The selected Owner package UUID.</param>
/// <param name="Version">The exact stable release.</param>
public sealed record GetPackageVersionQuery(Guid PackageId, string Version) : IRequest<ApiResponse<VersionResponseDto>>, IAdminRequest;
