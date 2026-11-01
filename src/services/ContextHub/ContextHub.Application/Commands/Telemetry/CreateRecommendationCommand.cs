namespace ContextHub.Application.Commands.Telemetry;

/// <summary>
/// Dispatches the protected CreateAsync use case.
/// </summary>
/// <param name="Request">The approved request input.</param>
public sealed record CreateRecommendationCommand(CreateRecommendationRequestDto Request) : IRequest<ApiResponse<RecommendationResponseDto>>, IAdminRequest;
