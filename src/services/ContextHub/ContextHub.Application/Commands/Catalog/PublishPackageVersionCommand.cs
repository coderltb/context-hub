namespace ContextHub.Application.Commands.Catalog;

/// <summary>
/// Defines the authorized PublishPackageVersion catalog operation.
/// </summary>
/// <param name="PackageId">The public package identifier.</param>
/// <param name="Request">The editable operation input.</param>
public sealed record PublishPackageVersionCommand(string PackageId, PublishVersionRequestDto Request) : IRequest<ApiResponse<VersionResponseDto>>, IAdminRequest;
