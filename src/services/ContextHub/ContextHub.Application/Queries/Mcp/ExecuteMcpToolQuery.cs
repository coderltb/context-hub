namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Carries schema-validated MCP arguments into the neutral application dispatch boundary.
/// </summary>
/// <param name="ToolName">The fixed public tool identifier.</param>
/// <param name="Arguments">The validated arguments with no HTTP or SDK dependency.</param>
public sealed record ExecuteMcpToolQuery(string ToolName, JsonElement Arguments) : IRequest<JsonObject>, IMcpRequest;
