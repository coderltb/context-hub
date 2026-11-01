namespace ContextHub.Infrastructure.Services.Mcp;

/// <summary>
/// Combines fixed definitions with current state without caching an authorization decision.
/// </summary>
/// <param name="toolRepository">The scoped authoritative state repository.</param>
public sealed class ToolStateService(IToolRepository toolRepository) : IToolStateService
{
    /// <summary>
    /// Reads mutable contracts without substituting embedded defaults for missing authority.
    /// </summary>
    /// <param name="toolName">The selected name, or null for discovery.</param>
    /// <param name="cancellationToken">Cancels the current database read.</param>
    /// <returns>The registered contracts in fixed discovery order.</returns>
    public async Task<IReadOnlyList<McpToolContractModel>> GetContractsAsync(
        string? toolName,
        CancellationToken cancellationToken)
    {
        var contracts = await toolRepository.GetContractsAsync(toolName, cancellationToken);
        var required = ToolCatalogConstants.TOOLS.Where(tool => toolName == null || tool.Name == toolName).ToArray();
        if (required.Any(tool => !contracts.Any(contract => contract.Name == tool.Name)))
        {
            throw new ToolStateUnavailableException();
        }

        return required.Select(tool => contracts.Single(contract => contract.Name == tool.Name)).ToArray();
    }

    /// <summary>
    /// Gets live discovery state in one query without reading Owner-only connection metadata.
    /// </summary>
    /// <param name="cancellationToken">Cancels the authoritative state read.</param>
    /// <returns>Enabled fixed names in their declared order.</returns>
    public async Task<IReadOnlyList<string>> GetEnabledNamesAsync(CancellationToken cancellationToken)
    {
        var states = await toolRepository.GetStatesAsync(cancellationToken);
        RequireCompleteState(states);
        var enabled = states.Where(value => value.IsEnabled).Select(value => value.Name).ToHashSet(StringComparer.Ordinal);
        return ToolCatalogConstants.TOOLS.Where(value => enabled.Contains(value.Name)).Select(value => value.Name).ToArray();
    }

    /// <summary>
    /// Returns all registered tools in their fixed order, including disabled entries.
    /// </summary>
    /// <param name="cancellationToken">Cancels state and routing metadata reads.</param>
    /// <returns>The complete Owner projection.</returns>
    public async Task<McpToolsResponseDto> GetListAsync(CancellationToken cancellationToken)
    {
        var states = await toolRepository.GetStatesAsync(cancellationToken);
        RequireCompleteState(states);

        var connections = await toolRepository.GetConnectionNamesAsync(cancellationToken);
        return new() { Items = ToolCatalogConstants.TOOLS.Select(definition => Project(states.Single(state => state.Name == definition.Name), definition, connections)).ToArray() };
    }

    /// <summary>
    /// Changes only one registered tool and returns its committed version.
    /// </summary>
    /// <param name="toolName">The fixed public name.</param>
    /// <param name="request">The desired state and expected version.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The saved approved tool metadata.</returns>
    public async Task<McpToolResponseDto> UpdateAsync(
        string toolName,
        UpdateMcpToolRequestDto request,
        CancellationToken cancellationToken)
    {
        var definition = ToolCatalogConstants.TOOLS.SingleOrDefault(value => value.Name == toolName)
            ?? throw new BusinessException(TelemetryErrorConstants.TOOL_NAME_INVALID, "The tool name is not registered.");
        var entity = await toolRepository.SaveStateAsync(toolName, request, cancellationToken);
        var connections = await toolRepository.GetConnectionNamesAsync(cancellationToken);
        return Project(entity.Adapt<ToolStateModel>(), definition, connections);
    }

    /// <summary>
    /// Rechecks the authoritative global row for each protected invocation.
    /// </summary>
    /// <param name="toolName">The fixed public name.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Whether the known tool is currently enabled.</returns>
    public async Task<bool> IsEnabledAsync(
        string toolName,
        CancellationToken cancellationToken)
    {
        if (!ToolCatalogConstants.TOOLS.Any(value => value.Name == toolName))
        {
            return false;
        }

        var states = await toolRepository.GetStatesAsync(cancellationToken);
        return (states.SingleOrDefault(value => value.Name == toolName) ?? throw new ToolStateUnavailableException()).IsEnabled;
    }

    /// <summary>
    /// Keeps incomplete provisioning from silently narrowing a public discovery result.
    /// </summary>
    /// <param name="states">The rows observed by one authority query.</param>
    private static void RequireCompleteState(IReadOnlyList<ToolStateModel> states)
    {
        if (ToolCatalogConstants.TOOLS.Any(tool => !states.Any(state => state.Name == tool.Name)))
        {
            throw new ToolStateUnavailableException();
        }
    }

    /// <summary>
    /// Adds only static definition metadata to an approved state projection.
    /// </summary>
    /// <param name="entity">The current database row.</param>
    /// <param name="definition">The registered public definition.</param>
    /// <param name="connections">Enabled Owner-only connection names.</param>
    /// <returns>The complete approved response.</returns>
    private static McpToolResponseDto Project(
        ToolStateModel entity,
        ToolDefinitionModel definition,
        IReadOnlyList<string> connections) =>
        entity.Adapt<McpToolResponseDto>() with
        {
            Implementation = definition.Name is "library_documentation_search" or "managed_tool_search" or "managed_tool_describe" or "managed_tool_call"
                ? new ToolImplementationResponseDto { ConnectionNames = connections }
                : null
        };
}
