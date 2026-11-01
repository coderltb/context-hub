namespace ContextHub.Authentication.Application.Commands.Clients.CreateClient;

/// <summary>
/// Owns application creation decisions while Infrastructure handles credentials, audit stamping and persistence.
/// </summary>
/// <param name="clientService">The Infrastructure service port using Application contracts.</param>
/// <param name="logger">The safe operation logger.</param>
public sealed class CreateClientCommandHandler(
    IClientService clientService,
    ILogger<CreateClientCommandHandler> logger)
    : IRequestHandler<CreateClientCommand, ApiResponse<IssuedClientTokenResponseDto>>
{
    /// <summary>
    /// Applies the authorized business operation without moving the workflow into an Infrastructure service.
    /// </summary>
    /// <param name="request">The validated use case input.</param>
    /// <param name="cancellationToken">Cancels staging, queries and the final save.</param>
    /// <returns>The common API envelope containing only the approved result.</returns>
    public async ValueTask<ApiResponse<IssuedClientTokenResponseDto>> Handle(
        CreateClientCommand request,
        CancellationToken cancellationToken)
    {
        // Mapping copies editable metadata; identity and initial state remain server-owned.
        var client = request.Request.Adapt<ClientModel>() with
        {
            ClientId = Guid.CreateVersion7(),
            IsActive = true
        };

        // The shared authorization behavior supplied the actor. Audit metadata belongs to SaveChanges, not the request body.
        var issued = await clientService.CreateAsync(client, request.Request.Adapt<IssueClientTokenRequestDto>(), cancellationToken);
        logger.LogInformation(
            AuthenticationLogConstants.TOKEN_ISSUED,
            client.ClientId,
            issued.TokenId,
            issued.AllowedTools.Count);

        return new(issued);
    }
}
