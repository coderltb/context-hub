namespace ContextHub.Application.Commands.Catalog;

/// <summary>
/// Defines the authorized DeprecatePackageVersion catalog operation.
/// </summary>
/// <param name="PackageId">The public package identifier.</param>
/// <param name="Version">The exact stable release.</param>
/// <param name="Request">The editable operation input.</param>
public sealed record DeprecatePackageVersionCommand(
    string PackageId,
    string Version,
    LifecycleVersionRequestDto Request) : IRequest<ApiResponse<VersionResponseDto>>, IAdminRequest;
