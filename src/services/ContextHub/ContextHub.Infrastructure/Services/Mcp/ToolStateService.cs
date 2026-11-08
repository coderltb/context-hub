namespace ContextHub.Infrastructure.Services.Mcp;

/// <summary>
/// Combines fixed definitions with current state without caching an authorization decision.
/// </summary>
/// <param name="toolRepository">The scoped authoritative state repository.</param>
public sealed class ToolStateService(IToolRepository toolRepository) : IToolStateService
{
    /// <summary>
    /// Reads one stored tool including both schemas, even when it is disabled.
    /// </summary>
    /// <param name="toolId">The immutable Owner-facing identity.</param>
    /// <param name="cancellationToken">Cancels the database read.</param>
    /// <returns>The current configuration.</returns>
    public async Task<McpToolDetailResponseDto> GetAsync(Guid toolId, CancellationToken cancellationToken)
    {
        // Owner inspection must not inherit the consumer's enabled-only discovery filter.
        var entity = await toolRepository.GetDetailAsync(toolId, cancellationToken)
            ?? throw new BusinessException(
                TelemetryErrorConstants.TOOL_NAME_INVALID,
                "The requested tool was not found.",
                BusinessErrorKind.NotFound);
        var definition = ToolCatalogConstants.TOOLS.Single(value => value.Name == entity.Name);
        var connections = await toolRepository.GetConnectionNamesAsync(cancellationToken);
        var metadata = Project(entity.Adapt<ToolStateModel>(), definition, connections);

        return entity.Adapt<McpToolDetailResponseDto>() with
        {
            Implementation = metadata.Implementation,
            Resources = McpResourceConstants.DEFINITIONS.Where(value => value.ToolName == entity.Name)
                .Select(value => new McpRelatedResourceResponseDto(value.Uri, value.Name)).ToArray()
        };
    }

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
    /// Returns one database page independently of unpaged public tool discovery.
    /// </summary>
    /// <param name="request">The Owner list filters.</param>
    /// <param name="cancellationToken">Cancels provider reads.</param>
    /// <returns>The safe metadata page.</returns>
    public async Task<PageResponse<McpToolResponseDto>> GetListAsync(
        GetMcpToolsRequestDto request,
        CancellationToken cancellationToken)
    {
        var page = await toolRepository.GetPageAsync(request, cancellationToken);
        var connections = page.Items.Count == 0 ? [] : await toolRepository.GetConnectionNamesAsync(cancellationToken);
        var items = page.Items.Select(value => Project(
            value,
            ToolCatalogConstants.TOOLS.Single(tool => tool.Name == value.Name),
            connections)).ToArray();

        return new(items, new PageRequest(page.Page, page.PageSize), page.TotalCount);
    }

    /// <summary>
    /// Applies an Owner update to the exact registered tool UUID.
    /// </summary>
    /// <param name="toolId">The stored tool identity.</param>
    /// <param name="request">The desired metadata, state and expected version.</param>
    /// <param name="cancellationToken">Cancels persistence and routing metadata reads.</param>
    /// <returns>The committed metadata without changing native handler identity.</returns>
    public async Task<McpToolResponseDto> UpdateAsync(
        Guid toolId,
        UpdateMcpToolRequestDto request,
        CancellationToken cancellationToken)
    {
        var entity = await toolRepository.SaveStateAsync(toolId, request, cancellationToken);
        var definition = ToolCatalogConstants.TOOLS.Single(value => value.Name == entity.Name);
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
