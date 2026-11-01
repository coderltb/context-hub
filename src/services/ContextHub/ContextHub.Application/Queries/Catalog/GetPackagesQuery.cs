namespace ContextHub.Application.Queries.Catalog;

/// <summary>
/// Defines the authorized GetPackages catalog operation.
/// </summary>
/// <param name="Request">The editable operation input.</param>
public sealed record GetPackagesQuery(PackageListRequestDto Request) : IRequest<ApiResponse<PageResponse<PackageResponseDto>>>, IAdminRequest;
