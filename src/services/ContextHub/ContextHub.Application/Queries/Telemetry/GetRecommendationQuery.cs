namespace ContextHub.Application.Queries.Telemetry;

/// <summary>
/// Dispatches the protected GetAsync use case.
/// </summary>
/// <param name="Id">The approved id input.</param>
public sealed record GetRecommendationQuery(Guid Id) : IRequest<ApiResponse<RecommendationResponseDto>>, IAdminRequest;
