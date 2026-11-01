namespace ContextHub.Infrastructure.Interfaces.Telemetry;

/// <summary>
/// Defines the feature-specific persistence boundary owned by Infrastructure.
/// </summary>
public interface IRecommendationRepository
{
    /// <summary>
    /// Executes the named authoritative persistence operation.
    /// </summary>
    /// <param name="request">The approved request input.</param>
    /// <param name="cancellationToken">The approved cancellationToken input.</param>
    /// <returns>The completed persistence operation or approved projection.</returns>
    Task<PageResponse<RecommendationResponseDto>> GetReviewPageAsync(GetRecommendationsRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the named authoritative persistence operation.
    /// </summary>
    /// <param name="id">The approved id input.</param>
    /// <param name="cancellationToken">The approved cancellationToken input.</param>
    /// <returns>The completed persistence operation or approved projection.</returns>
    Task<RecommendationResponseDto> GetReviewAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the named authoritative persistence operation.
    /// </summary>
    /// <param name="request">The approved request input.</param>
    /// <param name="cancellationToken">The approved cancellationToken input.</param>
    /// <returns>The completed persistence operation or approved projection.</returns>
    Task<RecommendationResponseDto> CreateReviewAsync(CreateRecommendationRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the named authoritative persistence operation.
    /// </summary>
    /// <param name="id">The approved id input.</param>
    /// <param name="request">The approved request input.</param>
    /// <param name="cancellationToken">The approved cancellationToken input.</param>
    /// <returns>The completed persistence operation or approved projection.</returns>
    Task<RecommendationResponseDto> ChangeStatusAsync(Guid id, UpdateRecommendationStatusRequestDto request, CancellationToken cancellationToken);
}
