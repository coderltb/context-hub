namespace ContextHub.Authentication.Application.Commands.Clients.IssueClientToken;

/// <summary>
/// Owns the issue client token workflow, state decisions and shared commit boundary.
/// </summary>
/// <param name="clientService">The Infrastructure service port using Application contracts.</param>
/// <param name="logger">The safe operation logger.</param>
public sealed class IssueClientTokenCommandHandler(
    IClientService clientService,
    ILogger<IssueClientTokenCommandHandler> logger) : IRequestHandler<IssueClientTokenCommand, ApiResponse<IssuedClientTokenResponseDto>>
{
    /// <summary>
    /// Applies the authorized business operation without moving the workflow into an Infrastructure service.
    /// </summary>
    /// <param name="request">The validated use case input.</param>
    /// <param name="cancellationToken">Cancels staging, queries and the final save.</param>
    /// <returns>The common API envelope containing only the approved result.</returns>
    public async ValueTask<ApiResponse<IssuedClientTokenResponseDto>> Handle(
        IssueClientTokenCommand request,
        CancellationToken cancellationToken)
    {
        var client = await clientService.GetAsync(request.ClientId, cancellationToken);

        if (client is null)
        {
            logger.LogWarning(
                AuthenticationLogConstants.OPERATION_REJECTED,
                nameof(IssueClientTokenCommand),
                AuthenticationErrorConstants.CLIENT_MISSING);
            throw new BusinessException(
                AuthenticationErrorConstants.CLIENT_MISSING,
                AuthenticationErrorConstants.CLIENT_UNAVAILABLE,
                BusinessErrorKind.NotFound);
        }

        if (!client.IsActive)
        {
            logger.LogWarning(
                AuthenticationLogConstants.OPERATION_REJECTED,
                nameof(IssueClientTokenCommand),
                AuthenticationErrorConstants.CLIENT_INACTIVE);
            throw new BusinessException(
                AuthenticationErrorConstants.CLIENT_INACTIVE,
                AuthenticationErrorConstants.CLIENT_UNAVAILABLE,
                BusinessErrorKind.Conflict);
        }

        // Rotation intentionally allows existing credentials to overlap until the administrator revokes them.
        var issued = await clientService.IssueAsync(client, request.Request, cancellationToken);
        logger.LogInformation(
            AuthenticationLogConstants.TOKEN_ISSUED,
            request.ClientId,
            issued.TokenId,
            issued.AllowedTools.Count);

        return new(issued);
    }
}
