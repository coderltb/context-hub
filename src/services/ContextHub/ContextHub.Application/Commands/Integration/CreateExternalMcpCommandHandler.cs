namespace ContextHub.Application.Commands.Integration;

/// <summary>
/// Dispatches creation to the owned persistence boundary.
/// </summary>
/// <param name="externalMcpService">The canonical connection port.</param>
public sealed class CreateExternalMcpCommandHandler(IExternalMcpService externalMcpService)
    : IRequestHandler<CreateExternalMcpCommand, ApiResponse<ExternalMcpResponseDto>>
{
    /// <summary>
    /// Returns the committed connection in the existing response envelope.
    /// </summary>
    /// <param name="request">The authorized and validated creation.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The committed Owner projection.</returns>
    public async ValueTask<ApiResponse<ExternalMcpResponseDto>> Handle(
        CreateExternalMcpCommand request,
        CancellationToken cancellationToken) =>
        new(await externalMcpService.CreateAsync(request.Request, cancellationToken));
}
