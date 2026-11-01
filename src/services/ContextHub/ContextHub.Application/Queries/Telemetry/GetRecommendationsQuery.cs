namespace ContextHub.Application.Queries.Telemetry;

/// <summary>
/// Dispatches the protected GetPageAsync use case.
/// </summary>
/// <param name="Request">The approved request input.</param>
public sealed record GetRecommendationsQuery(GetRecommendationsRequestDto Request) : IRequest<ApiResponse<PageResponse<RecommendationResponseDto>>>, IAdminRequest;
