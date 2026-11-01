namespace ContextHub.Application.Queries.Integration;

/// <summary>
/// Dispatches the authorized GetExternalMcpsQuery use case through the existing Mediator pipeline.
/// </summary>
public sealed record GetExternalMcpsQuery : IRequest<ApiResponse<ExternalMcpListResponseDto>>, IAdminRequest;
