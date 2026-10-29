namespace ContextHub.Authentication.Application.Queries.Clients.GetClients;

/// <summary>
/// Dispatches the authorized use case to the Authentication-owned credential boundary.
/// </summary>
/// <param name="clientService">The Infrastructure service port using approved contracts.</param>
public sealed class GetClientsQueryHandler(IClientService clientService)
    : IRequestHandler<GetClientsQuery, ApiResponse<PageResponse<ClientResponseDto>>>
{
    /// <summary>
    /// Runs the operation after the shared authorization and validation behaviors.
    /// </summary>
    /// <param name="request">The typed use case input.</param>
    /// <param name="cancellationToken">Cancels the owned persistence work.</param>
    /// <returns>The approved response without database entities.</returns>
    public async ValueTask<ApiResponse<PageResponse<ClientResponseDto>>> Handle(
        GetClientsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await clientService.GetPageAsync(request.Request, cancellationToken);

        return new(result);
    }
}
