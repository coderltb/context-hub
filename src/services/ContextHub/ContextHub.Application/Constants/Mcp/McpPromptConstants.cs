namespace ContextHub.Application.Constants.Mcp;

/// <summary>
/// Owns prompt limits and safe rejections shared by administration and native protocol adapters.
/// </summary>
public static class McpPromptConstants
{
    /// <summary>
    /// Introduces the current capability list without persisting a caller-specific prompt.
    /// </summary>
    public const string AVAILABLE_CAPABILITIES_PREFIX = " Available capabilities: ";

    /// <summary>
    /// Keeps guidance from treating a disabled capability as reachable through a resource.
    /// </summary>
    public const string CAPABILITY_GUIDANCE = ". Use only these capabilities; unavailable flows must stop without a resource bypass.";

    /// <summary>
    /// Separates transient consumer arguments from stored guidance.
    /// </summary>
    public const string REQUESTED_INPUTS_PREFIX = "\nRequested inputs: ";

    /// <summary>
    /// Limits the stable prompt identifier.
    /// </summary>
    public const int MAX_NAME_LENGTH = 128;

    /// <summary>
    /// Identifies the reviewed unique-name constraint for narrow conflict translation.
    /// </summary>
    public const string NAME_CONSTRAINT = "UQ_Access_Prompt_Name";

    /// <summary>
    /// Limits each submitted string without persisting client input.
    /// </summary>
    public const int MAX_ARGUMENT_LENGTH = 512;

    /// <summary>
    /// Identifies an unavailable prompt.
    /// </summary>
    public const string NOT_FOUND = "mcp_prompt_not_found";

    /// <summary>
    /// Identifies a duplicate name or stale update.
    /// </summary>
    public const string CONFLICT = "mcp_prompt_conflict";

    /// <summary>
    /// Identifies invalid submitted prompt arguments.
    /// </summary>
    public const string ARGUMENTS_INVALID = "mcp_prompt_arguments_invalid";

    /// <summary>
    /// Provides a safe missing-prompt message.
    /// </summary>
    public const string NOT_FOUND_MESSAGE = "The prompt is not available.";

    /// <summary>
    /// Provides a safe invalid-input message.
    /// </summary>
    public const string ARGUMENTS_INVALID_MESSAGE = "The prompt arguments are invalid.";

    /// <summary>
    /// Provides a safe concurrent-write message.
    /// </summary>
    public const string CONFLICT_MESSAGE = "The prompt configuration has changed or its name already exists.";

    /// <summary>
    /// Defines the public ordering allowlist.
    /// </summary>
    public static readonly IReadOnlyList<string> SORT_FIELDS = ["name", "updatedAt"];
}
