namespace ContextHub.Application.Constants.Mcp;

/// <summary>
/// Defines shared MCP input failures without provider or credential details.
/// </summary>
public static class McpErrorConstants
{
    /// <summary>
    /// Identifies a disabled capability.
    /// </summary>
    public const string TOOL_NOT_ENABLED = "tool_not_enabled";

    /// <summary>
    /// Identifies a required unavailable dependency.
    /// </summary>
    public const string DEPENDENCY_UNAVAILABLE = "dependency_unavailable";

    /// <summary>
    /// Identifies an unavailable managed search index.
    /// </summary>
    public const string MANAGED_TOOL_INDEX_UNAVAILABLE = "managed_tool_index_unavailable";

    /// <summary>
    /// Identifies an unavailable managed definition registry.
    /// </summary>
    public const string MANAGED_TOOL_REGISTRY_UNAVAILABLE = "managed_tool_registry_unavailable";

    /// <summary>
    /// Identifies an unavailable managed operation.
    /// </summary>
    public const string MANAGED_TOOL_UNAVAILABLE = "managed_tool_unavailable";

    /// <summary>
    /// Identifies input rejected by the managed-tool contract.
    /// </summary>
    public const string MANAGED_TOOL_INPUT_INVALID = "managed_tool_input_invalid";

    /// <summary>
    /// Identifies an unavailable reviewed documentation provider.
    /// </summary>
    public const string DOCUMENTATION_PROVIDER_UNAVAILABLE = "documentation_provider_unavailable";

    /// <summary>
    /// Identifies an exhausted feedback submission allowance.
    /// </summary>
    public const string RATE_LIMITED = "rate_limited";

    /// <summary>
    /// Identifies a result that cannot be published safely.
    /// </summary>
    public const string INTERNAL_ERROR = "internal_error";

    /// <summary>
    /// Describes a currently disabled capability.
    /// </summary>
    public const string CAPABILITY_UNAVAILABLE = "This capability is currently unavailable.";

    /// <summary>
    /// Describes an unsupported fixed capability.
    /// </summary>
    public const string UNSUPPORTED_CAPABILITY = "The requested capability is not supported.";

    /// <summary>
    /// Describes a safe failure without exception-owned text.
    /// </summary>
    public const string REQUEST_FAILED = "The request could not be completed.";

    /// <summary>
    /// Describes a recoverable dependency failure.
    /// </summary>
    public const string DEPENDENCY_UNAVAILABLE_MESSAGE = "A required dependency is unavailable.";

    /// <summary>
    /// Describes an exhausted feedback allowance.
    /// </summary>
    public const string SUBMISSION_LIMIT_REACHED = "The submission limit has been reached.";

    /// <summary>
    /// Describes input rejected against the advertised contract.
    /// </summary>
    public const string SCHEMA_INPUT_INVALID = "The tool input does not match its schema.";

    /// <summary>
    /// Describes output rejected against the advertised contract.
    /// </summary>
    public const string RESULT_UNAVAILABLE = "The result could not be returned safely.";

    /// <summary>
    /// Summarizes successful structured content without duplicating its payload.
    /// </summary>
    public const string CAPABILITY_COMPLETED = "The requested capability completed.";

    /// <summary>
    /// Identifies invalid input on the existing MCP wire contract.
    /// </summary>
    public const string VALIDATION_ERROR = "validation_error";

    /// <summary>
    /// Describes input that cannot be consumed by the selected handler.
    /// </summary>
    public const string INVALID_INPUT = "The tool input does not match its handler contract.";
}
