namespace ContextHub.Application.Commands.Integration;

/// <summary>
/// Dispatches the authorized PreviewExternalMcpsCommand use case through the existing Mediator pipeline.
/// </summary>
/// <param name="Import">The validated Import input.</param>
public sealed record PreviewExternalMcpsCommand(JsonElement Import) : IRequest<ApiResponse<ExternalMcpPreviewResponseDto>>, IAdminRequest;
