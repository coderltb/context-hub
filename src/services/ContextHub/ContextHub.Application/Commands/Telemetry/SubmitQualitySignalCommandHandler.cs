namespace ContextHub.Application.Commands.Telemetry;

/// <summary>
/// Owns the authenticated submission budget and result while Infrastructure performs atomic IO.
/// </summary>
/// <param name="qualitySignalService">The Redis and PostgreSQL operation port.</param>
/// <param name="authService">The principal already checked by the shared MCP authorization policy.</param>
public sealed class SubmitQualitySignalCommandHandler(
    IQualitySignalService qualitySignalService,
    IAuthService authService)
    : IRequestHandler<SubmitQualitySignalCommand, QualitySignalResponseDto>
{
    /// <summary>
    /// Applies the shared client budget before persisting one validated coded signal.
    /// </summary>
    /// <param name="request">The request accepted by the authorization and validation behaviors.</param>
    /// <param name="cancellationToken">Cancels coordination and persistence.</param>
    /// <returns>The accepted or deduplicated outcome after persistence succeeds.</returns>
    public async ValueTask<QualitySignalResponseDto> Handle(
        SubmitQualitySignalCommand request,
        CancellationToken cancellationToken)
    {
        // Identity comes from the protected principal; a consumer cannot select another client's budget.
        var clientId = authService.Get<Guid>(McpApplicationConstants.CLIENT_ID);
        var count = await qualitySignalService.IncrementBudgetAsync(clientId, cancellationToken);
        if (count > TelemetryConstants.QUALITY_SIGNALS_PER_HOUR)
        {
            throw new QualitySignalRateLimitException();
        }

        // A successful or duplicate report consumes the existing window without resetting its counter.
        var inserted = await qualitySignalService.PersistAsync(request.Request, clientId, cancellationToken);

        return new QualitySignalResponseDto
        {
            Status = inserted ? TelemetryConstants.SIGNAL_ACCEPTED : TelemetryConstants.SIGNAL_DEDUPLICATED
        };
    }
}
