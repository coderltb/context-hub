namespace ContextHub.Application.Commands.Catalog;

/// <summary>
/// Defines the authorized UpdatePackage catalog operation.
/// </summary>
/// <param name="PackageId">The selected Owner package UUID.</param>
/// <param name="Request">The editable operation input.</param>
public sealed record UpdatePackageCommand(Guid PackageId, UpdatePackageRequestDto Request) : IRequest<ApiResponse<PackageResponseDto>>, IAdminRequest;
