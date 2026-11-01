namespace ContextHub.Application.Commands.Telemetry;

/// <summary>
/// Submits transient coded feedback using the authenticated MCP client identity.
/// </summary>
/// <param name="Request">The fixed-code signal without caller-supplied identity or task content.</param>
public sealed record SubmitQualitySignalCommand(QualitySignalRequestDto Request)
    : IRequest<QualitySignalResponseDto>, IMcpRequest
{
    /// <summary>
    /// Gets the exact capability grant required by the shared authorization behavior.
    /// </summary>
    public string ToolName => ToolCatalogConstants.QUALITY_SIGNAL_SUBMIT;
}
