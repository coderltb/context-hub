namespace ContextHub.Application.Commands.Catalog;

/// <summary>
/// Defines the authorized PreviewPackageVersion catalog operation.
/// </summary>
/// <param name="PackageId">The selected Owner package UUID.</param>
/// <param name="Request">The editable operation input.</param>
public sealed record PreviewPackageVersionCommand(Guid PackageId, PublishVersionRequestDto Request) : IRequest<ApiResponse<PackagePreviewResponseDto>>, IAdminRequest;
