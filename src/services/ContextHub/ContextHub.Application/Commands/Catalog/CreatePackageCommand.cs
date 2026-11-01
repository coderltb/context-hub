namespace ContextHub.Application.Commands.Catalog;

/// <summary>
/// Defines the authorized CreatePackage catalog operation.
/// </summary>
/// <param name="Request">The editable operation input.</param>
public sealed record CreatePackageCommand(CreatePackageRequestDto Request) : IRequest<ApiResponse<PackageResponseDto>>, IAdminRequest;
