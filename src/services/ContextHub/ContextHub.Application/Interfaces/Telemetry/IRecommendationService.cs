namespace ContextHub.Application.Interfaces.Telemetry;

/// <summary>
/// Defines an Application-owned port for privacy-safe feature operations.
/// </summary>
public interface IRecommendationService
{
    /// <summary>
    /// Gets a provider-filtered review page.
    /// </summary>
    /// <param name="request">The approved request input.</param>
    /// <param name="cancellationToken">Cancels the owned database or coordination operation.</param>
    /// <returns>The completed operation or approved result.</returns>
    Task<PageResponse<RecommendationResponseDto>> GetPageAsync(GetRecommendationsRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one approved review item.
    /// </summary>
    /// <param name="id">The approved id input.</param>
    /// <param name="cancellationToken">Cancels the owned database or coordination operation.</param>
    /// <returns>The completed operation or approved result.</returns>
    Task<RecommendationResponseDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates manual Owner feedback.
    /// </summary>
    /// <param name="request">The approved request input.</param>
    /// <param name="cancellationToken">Cancels the owned database or coordination operation.</param>
    /// <returns>The completed operation or approved result.</returns>
    Task<RecommendationResponseDto> CreateAsync(CreateRecommendationRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a permitted transition against the expected version.
    /// </summary>
    /// <param name="id">The approved id input.</param>
    /// <param name="request">The approved request input.</param>
    /// <param name="cancellationToken">Cancels the owned database or coordination operation.</param>
    /// <returns>The completed operation or approved result.</returns>
    Task<RecommendationResponseDto> UpdateStatusAsync(Guid id, UpdateRecommendationStatusRequestDto request, CancellationToken cancellationToken);
}
