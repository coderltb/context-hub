namespace ContextHub.Application.Commands.Catalog;

/// <summary>
/// Defines the authorized PublishPackageVersion catalog operation.
/// </summary>
/// <param name="PackageId">The selected Owner package UUID.</param>
/// <param name="Request">The editable operation input.</param>
public sealed record PublishPackageVersionCommand(Guid PackageId, PublishVersionRequestDto Request) : IRequest<ApiResponse<VersionResponseDto>>, IAdminRequest;
