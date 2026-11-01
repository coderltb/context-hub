namespace ContextHub.Application.Commands.Catalog;

/// <summary>
/// Defines the authorized UpdatePackage catalog operation.
/// </summary>
/// <param name="PackageId">The public package identifier.</param>
/// <param name="Request">The editable operation input.</param>
public sealed record UpdatePackageCommand(string PackageId, UpdatePackageRequestDto Request) : IRequest<ApiResponse<PackageResponseDto>>, IAdminRequest;
