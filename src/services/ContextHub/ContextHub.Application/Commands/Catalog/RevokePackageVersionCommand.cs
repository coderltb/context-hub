namespace ContextHub.Application.Commands.Catalog;

/// <summary>
/// Defines the authorized RevokePackageVersion catalog operation.
/// </summary>
/// <param name="PackageId">The selected Owner package UUID.</param>
/// <param name="Version">The exact stable release.</param>
/// <param name="Request">The editable operation input.</param>
public sealed record RevokePackageVersionCommand(
    Guid PackageId,
    string Version,
    LifecycleVersionRequestDto Request) : IRequest<ApiResponse<VersionResponseDto>>, IAdminRequest;
