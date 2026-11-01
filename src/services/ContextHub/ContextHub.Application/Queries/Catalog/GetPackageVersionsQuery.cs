namespace ContextHub.Application.Queries.Catalog;

/// <summary>
/// Defines the authorized GetPackageVersions catalog operation.
/// </summary>
/// <param name="PackageId">The public package identifier.</param>
/// <param name="Request">The editable operation input.</param>
public sealed record GetPackageVersionsQuery(string PackageId, VersionListRequestDto Request) : IRequest<ApiResponse<PageResponse<VersionSummaryResponseDto>>>, IAdminRequest;
