namespace ContextHub.Application.Queries.Catalog;

/// <summary>
/// Defines the authorized GetPackageVersions catalog operation.
/// </summary>
/// <param name="PackageId">The selected Owner package UUID.</param>
/// <param name="Request">The editable operation input.</param>
public sealed record GetPackageVersionsQuery(Guid PackageId, VersionListRequestDto Request) : IRequest<ApiResponse<PageResponse<VersionSummaryResponseDto>>>, IAdminRequest;
