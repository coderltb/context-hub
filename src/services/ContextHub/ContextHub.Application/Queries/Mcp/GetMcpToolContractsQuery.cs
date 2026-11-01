namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Requests the current contracts after validating the client's capability grant.
/// </summary>
/// <param name="ToolName">An exact capability, or empty for filtered discovery.</param>
public sealed record GetMcpToolContractsQuery(string ToolName = "")
    : IRequest<IReadOnlyList<McpToolContractModel>>, IMcpRequest;
