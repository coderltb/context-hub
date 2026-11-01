namespace ContextHub.Infrastructure.Services.Telemetry;

/// <summary>
/// Keeps review persistence behind Application contracts and a specific repository.
/// </summary>
/// <param name="recommendationRepository">The scoped review repository.</param>
public sealed class RecommendationService(IRecommendationRepository recommendationRepository) : IRecommendationService
{
    /// <summary>
    /// Gets the filtered provider-projected review page.
    /// </summary>
    /// <param name="request">The validated list input.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The safe review page.</returns>
    public Task<PageResponse<RecommendationResponseDto>> GetPageAsync(GetRecommendationsRequestDto request, CancellationToken cancellationToken) =>
        recommendationRepository.GetReviewPageAsync(request, cancellationToken);

    /// <summary>
    /// Gets one approved queue item.
    /// </summary>
    /// <param name="id">The queue identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The safe review detail.</returns>
    public Task<RecommendationResponseDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        recommendationRepository.GetReviewAsync(id, cancellationToken);

    /// <summary>
    /// Persists approved manual feedback without changing catalog state.
    /// </summary>
    /// <param name="request">The editable manual fields.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The committed review item.</returns>
    public Task<RecommendationResponseDto> CreateAsync(CreateRecommendationRequestDto request, CancellationToken cancellationToken) =>
        recommendationRepository.CreateReviewAsync(request, cancellationToken);

    /// <summary>
    /// Writes a permitted state change under the expected native version.
    /// </summary>
    /// <param name="id">The queue identifier.</param>
    /// <param name="request">The target state and expected version.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The updated approved item.</returns>
    public Task<RecommendationResponseDto> UpdateStatusAsync(Guid id, UpdateRecommendationStatusRequestDto request, CancellationToken cancellationToken) =>
        recommendationRepository.ChangeStatusAsync(id, request, cancellationToken);
}
