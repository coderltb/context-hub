namespace ContextHub.Authentication.Application.Commands.Clients.RevokeClientToken;

/// <summary>
/// Owns the revoke client token workflow, state decisions and shared commit boundary.
/// </summary>
/// <param name="clientService">The Infrastructure service port using Application contracts.</param>
/// <param name="logger">The safe operation logger.</param>
public sealed class RevokeClientTokenCommandHandler(
    IClientService clientService,
    ILogger<RevokeClientTokenCommandHandler> logger) : IRequestHandler<RevokeClientTokenCommand, ApiResponse<ClientTokenResponseDto>>
{
    /// <summary>
    /// Applies the authorized business operation without moving the workflow into an Infrastructure service.
    /// </summary>
    /// <param name="request">The validated use case input.</param>
    /// <param name="cancellationToken">Cancels staging, queries and the final save.</param>
    /// <returns>The common API envelope containing only the approved result.</returns>
    public async ValueTask<ApiResponse<ClientTokenResponseDto>> Handle(
        RevokeClientTokenCommand request,
        CancellationToken cancellationToken)
    {
        // Bind both identifiers in the lookup so a token cannot be revoked through a different client's route.
        var token = await clientService.GetTokenAsync(request.ClientId, request.TokenId, cancellationToken);

        if (token is null)
        {
            logger.LogWarning(
                AuthenticationLogConstants.OPERATION_REJECTED,
                nameof(RevokeClientTokenCommand),
                AuthenticationErrorConstants.TOKEN_MISSING);
            throw new BusinessException(
                AuthenticationErrorConstants.TOKEN_MISSING,
                AuthenticationErrorConstants.TOKEN_UNAVAILABLE,
                BusinessErrorKind.NotFound);
        }

        // The service preserves an existing revocation; audit stamping and persistence stay below this business workflow.
        var result = await clientService.RevokeAsync(token, cancellationToken);
        logger.LogInformation(
            AuthenticationLogConstants.TOKEN_REVOKED,
            request.ClientId,
            result.TokenId,
            result.RevokedAt);

        return new(result);
    }
}
