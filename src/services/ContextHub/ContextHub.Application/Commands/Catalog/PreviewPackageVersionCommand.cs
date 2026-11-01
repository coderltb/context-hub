namespace ContextHub.Application.Commands.Catalog;

/// <summary>
/// Defines the authorized PreviewPackageVersion catalog operation.
/// </summary>
/// <param name="PackageId">The public package identifier.</param>
/// <param name="Request">The editable operation input.</param>
public sealed record PreviewPackageVersionCommand(string PackageId, PublishVersionRequestDto Request) : IRequest<ApiResponse<PackagePreviewResponseDto>>, IAdminRequest;
