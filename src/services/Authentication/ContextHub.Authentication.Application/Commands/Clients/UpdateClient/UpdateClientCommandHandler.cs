namespace ContextHub.Authentication.Application.Commands.Clients.UpdateClient;

/// <summary>
/// Owns the update client workflow, state decisions and shared commit boundary.
/// </summary>
/// <param name="clientService">The Infrastructure service port using Application contracts.</param>
/// <param name="logger">The safe operation logger.</param>
public sealed class UpdateClientCommandHandler(
    IClientService clientService,
    ILogger<UpdateClientCommandHandler> logger) : IRequestHandler<UpdateClientCommand, ApiResponse<ClientResponseDto>>
{
    /// <summary>
    /// Applies the authorized business operation without moving the workflow into an Infrastructure service.
    /// </summary>
    /// <param name="request">The validated use case input.</param>
    /// <param name="cancellationToken">Cancels staging, queries and the final save.</param>
    /// <returns>The common API envelope containing only the approved result.</returns>
    public async ValueTask<ApiResponse<ClientResponseDto>> Handle(
        UpdateClientCommand request,
        CancellationToken cancellationToken)
    {
        var client = await clientService.GetAsync(request.ClientId, cancellationToken);

        if (client is null)
        {
            logger.LogWarning(
                AuthenticationLogConstants.OPERATION_REJECTED,
                nameof(UpdateClientCommand),
                AuthenticationErrorConstants.CLIENT_MISSING);
            throw new BusinessException(
                AuthenticationErrorConstants.CLIENT_MISSING,
                AuthenticationErrorConstants.CLIENT_UNAVAILABLE,
                BusinessErrorKind.NotFound);
        }

        // The mapping file owns the persistence version's wire representation; the use case compares opaque values.
        if (!string.Equals(client.ConcurrencyToken, request.Request.ConcurrencyToken, StringComparison.Ordinal))
        {
            logger.LogWarning(
                AuthenticationLogConstants.OPERATION_REJECTED,
                nameof(UpdateClientCommand),
                AuthenticationErrorConstants.CONCURRENCY_CONFLICT);
            throw new BusinessException(
                AuthenticationErrorConstants.CONCURRENCY_CONFLICT,
                AuthenticationErrorConstants.ENTITY_CHANGED,
                BusinessErrorKind.Conflict);
        }

        client = client with
        {
            DisplayName = request.Request.DisplayName.Trim(),
            IsActive = request.Request.IsActive
        };
        var result = await clientService.UpdateAsync(client, cancellationToken);
        logger.LogInformation(AuthenticationLogConstants.CLIENT_UPDATED, result.ClientId, result.IsActive);

        return new(result);
    }
}
