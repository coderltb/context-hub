namespace ContextHub.Authentication.Application.Queries.Clients.GetClient;

/// <summary>
/// Dispatches the authorized use case to the Authentication-owned credential boundary.
/// </summary>
/// <param name="clientService">The Infrastructure service port using approved contracts.</param>
/// <param name="logger">The safe request and outcome logger.</param>
public sealed class GetClientQueryHandler(
    IClientService clientService,
    ILogger<GetClientQueryHandler> logger)
    : IRequestHandler<GetClientQuery, ApiResponse<ClientDetailResponseDto>>
{
    /// <summary>
    /// Runs the operation after the shared authorization and validation behaviors.
    /// </summary>
    /// <param name="request">The typed use case input.</param>
    /// <param name="cancellationToken">Cancels the owned persistence work.</param>
    /// <returns>The approved response without database entities.</returns>
    public async ValueTask<ApiResponse<ClientDetailResponseDto>> Handle(
        GetClientQuery request,
        CancellationToken cancellationToken)
    {
        var result = await clientService.GetDetailAsync(request.ClientId, cancellationToken);

        if (result is null)
        {
            logger.LogWarning(
                AuthenticationLogConstants.OPERATION_REJECTED,
                nameof(GetClientQuery),
                AuthenticationErrorConstants.CLIENT_MISSING);
            throw new BusinessException(
                AuthenticationErrorConstants.CLIENT_MISSING,
                AuthenticationErrorConstants.CLIENT_UNAVAILABLE,
                BusinessErrorKind.NotFound);
        }

        return new(result);
    }
}
