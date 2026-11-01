namespace ContextHub.Application.Commands.Telemetry;

/// <summary>
/// Owns the use-case result while Infrastructure performs scoped persistence.
/// </summary>
/// <param name="featureService">The feature port receiving Application contracts only.</param>
public sealed class UpdateRecommendationStatusCommandHandler(IRecommendationService featureService) : IRequestHandler<UpdateRecommendationStatusCommand, ApiResponse<RecommendationResponseDto>>
{
    /// <summary>
    /// Executes the authorized operation and creates the shared envelope once.
    /// </summary>
    /// <param name="request">The validated use-case input.</param>
    /// <param name="cancellationToken">Cancels the feature operation.</param>
    /// <returns>The approved result in the common envelope.</returns>
    public async ValueTask<ApiResponse<RecommendationResponseDto>> Handle(
        UpdateRecommendationStatusCommand request,
        CancellationToken cancellationToken)
    {
        var current = await featureService.GetAsync(request.Id, cancellationToken);
        if (!RecommendationWorkflowPolicy.CanTransition(current.Status, request.Request.Status))
        {
            throw new BusinessException(
                "recommendation_transition_invalid",
                "The requested workflow transition is not permitted.",
                BusinessErrorKind.Conflict);
        }

        // The expected version still guards the authoritative write after this policy decision.
        var result = await featureService.UpdateStatusAsync(request.Id, request.Request, cancellationToken);

        return new(result);
    }
}
