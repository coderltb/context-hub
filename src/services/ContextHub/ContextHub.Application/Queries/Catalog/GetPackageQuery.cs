namespace ContextHub.Application.Queries.Catalog;

/// <summary>
/// Defines the authorized GetPackage catalog operation.
/// </summary>
/// <param name="PackageId">The selected Owner package UUID.</param>
public sealed record GetPackageQuery(Guid PackageId) : IRequest<ApiResponse<PackageResponseDto>>, IAdminRequest;
