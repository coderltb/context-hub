namespace ContextHub.Application.Queries.Catalog;

/// <summary>
/// Defines the authorized SearchCatalog catalog operation.
/// </summary>
/// <param name="Request">The editable operation input.</param>
public sealed record SearchCatalogQuery(CatalogSearchRequestDto Request) : IRequest<ApiResponse<AdminCatalogSearchResponseDto>>, IAdminRequest;
