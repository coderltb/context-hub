namespace ContextHub.Application.Queries.Catalog;

/// <summary>
/// Defines the authorized GetPackage catalog operation.
/// </summary>
/// <param name="PackageId">The public package identifier.</param>
public sealed record GetPackageQuery(string PackageId) : IRequest<ApiResponse<PackageResponseDto>>, IAdminRequest;
