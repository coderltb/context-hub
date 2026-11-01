namespace ContextHub.Application.Interfaces;

/// <summary>
/// Marks a tool invocation requiring the authenticated application grant before dispatch.
/// </summary>
public interface IMcpRequest : IRequiresAuthorization
{
    /// <summary>
    /// Gets the fixed public tool name whose grant must be present.
    /// </summary>
    string ToolName { get; }
}
