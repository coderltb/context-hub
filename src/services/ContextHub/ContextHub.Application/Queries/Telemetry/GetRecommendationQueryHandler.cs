namespace ContextHub.Application.Queries.Telemetry;

/// <summary>
/// Owns the use-case result while Infrastructure performs scoped persistence.
/// </summary>
/// <param name="featureService">The feature port receiving Application contracts only.</param>
public sealed class GetRecommendationQueryHandler(IRecommendationService featureService) : IRequestHandler<GetRecommendationQuery, ApiResponse<RecommendationResponseDto>>
{
    /// <summary>
    /// Executes the authorized operation and creates the shared envelope once.
    /// </summary>
    /// <param name="request">The validated use-case input.</param>
    /// <param name="cancellationToken">Cancels the feature operation.</param>
    /// <returns>The approved result in the common envelope.</returns>
    public async ValueTask<ApiResponse<RecommendationResponseDto>> Handle(
        GetRecommendationQuery request,
        CancellationToken cancellationToken)
    {
        var result = await featureService.GetAsync(request.Id, cancellationToken);

        return new(result);
    }
}
