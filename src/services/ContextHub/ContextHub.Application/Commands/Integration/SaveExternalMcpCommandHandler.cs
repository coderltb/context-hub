namespace ContextHub.Application.Commands.Integration;

/// <summary>
/// Returns the approved integration result after shared authorization and validation.
/// </summary>
/// <param name="externalMcpService">The application-owned integration port.</param>
public sealed class SaveExternalMcpCommandHandler(IExternalMcpService externalMcpService)
    : IRequestHandler<SaveExternalMcpCommand, ApiResponse<ExternalMcpResponseDto>>
{
    /// <summary>
    /// Delegates the validated operation without exposing persistence entities.
    /// </summary>
    /// <param name="request">The authorized integration operation.</param>
    /// <param name="cancellationToken">Cancels the underlying IO boundary.</param>
    /// <returns>The approved metadata in the common response envelope.</returns>
    public async ValueTask<ApiResponse<ExternalMcpResponseDto>> Handle(
        SaveExternalMcpCommand request,
        CancellationToken cancellationToken) =>
        new(await externalMcpService.SaveAsync(request.Name, request.Request, cancellationToken));
}
