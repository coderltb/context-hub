namespace ContextHub.Application.Commands.Telemetry;

/// <summary>
/// Dispatches the protected UpdateStatusAsync use case.
/// </summary>
/// <param name="Id">The approved id input.</param>
/// <param name="Request">The approved request input.</param>
public sealed record UpdateRecommendationStatusCommand(Guid Id, UpdateRecommendationStatusRequestDto Request) : IRequest<ApiResponse<RecommendationResponseDto>>, IAdminRequest;
