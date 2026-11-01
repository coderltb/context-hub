namespace ContextHub.Application.Queries.Telemetry;

/// <summary>
/// Owns the use-case result while Infrastructure performs scoped persistence.
/// </summary>
/// <param name="featureService">The feature port receiving Application contracts only.</param>
public sealed class GetDashboardSummaryQueryHandler(IDashboardService featureService) : IRequestHandler<GetDashboardSummaryQuery, ApiResponse<DashboardSummaryResponseDto>>
{
    /// <summary>
    /// Executes the authorized operation and creates the shared envelope once.
    /// </summary>
    /// <param name="request">The validated use-case input.</param>
    /// <param name="cancellationToken">Cancels the feature operation.</param>
    /// <returns>The approved result in the common envelope.</returns>
    public async ValueTask<ApiResponse<DashboardSummaryResponseDto>> Handle(
        GetDashboardSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var to = request.Request.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.Request.From ?? to.AddDays(-29);

        // Resolve defaults before Infrastructure performs any aggregate reads.
        var result = await featureService.GetSummaryAsync(
            new DashboardRequestDto {
                From = from,
                To = to
            },
            cancellationToken);

        return new(result);
    }
}
