namespace ContextHub.Infrastructure.Repositories.Mcp;

/// <summary>
/// Keeps authoritative global tool state in the service context and reuses generic tracking and staging.
/// </summary>
/// <param name="contextHubDbContext">The sole scoped Context Hub context.</param>
/// <param name="unitOfWork">The matching save boundary.</param>
/// <param name="logger">The enclosing repository metadata logger.</param>
public sealed class ToolRepository(
    ContextHubDbContext contextHubDbContext,
    IUnitOfWork unitOfWork,
    ILogger<ToolRepository> logger) : GenericRepository<Tool>(contextHubDbContext, logger), IToolRepository
{
    /// <summary>
    /// Reads current state without retaining tracked entries for discovery.
    /// </summary>
    /// <param name="cancellationToken">Cancels the authoritative read.</param>
    /// <returns>The live rows.</returns>
    public Task<IReadOnlyList<ToolStateModel>> GetStatesAsync(CancellationToken cancellationToken) =>
        GetListAsync<ToolStateModel>(cancellationToken: cancellationToken);

    /// <summary>
    /// Reads paired contracts through the shared no-tracking projection pipeline.
    /// </summary>
    /// <param name="toolName">The optional exact registered name.</param>
    /// <param name="cancellationToken">Cancels the database read.</param>
    /// <returns>The contract fields from one database statement.</returns>
    public Task<IReadOnlyList<McpToolContractModel>> GetContractsAsync(
        string? toolName,
        CancellationToken cancellationToken) =>
        GetListAsync<McpToolContractModel>(
            predicate: toolName == null ? null : value => value.Name == toolName,
            cancellationToken: cancellationToken);

    /// <summary>
    /// Reads only connection metadata needed by Owner tool administration.
    /// </summary>
    /// <param name="cancellationToken">Cancels the metadata read.</param>
    /// <returns>Enabled connection names without configuration values.</returns>
    public async Task<IReadOnlyList<string>> GetConnectionNamesAsync(CancellationToken cancellationToken)
    {
        var connections = await contextHubDbContext.ExternalMcps.AsNoTracking()
            .Select(value => new { value.Name, value.Config }).ToArrayAsync(cancellationToken);

        return connections.Where(value => JsonNode.Parse(value.Config)?["enabled"]?.GetValue<bool>() == true)
            .Select(value => value.Name).Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Updates one tracked row using the caller's expected xmin value.
    /// </summary>
    /// <param name="toolName">The registered public name.</param>
    /// <param name="request">The desired state and expected version.</param>
    /// <param name="cancellationToken">Cancels lookup and save.</param>
    /// <returns>The newly committed state.</returns>
    public async Task<Tool> SaveStateAsync(
        string toolName,
        UpdateMcpToolRequestDto request,
        CancellationToken cancellationToken)
    {
        var expected = PersistenceConcurrencyHelper.Decode(request.ConcurrencyToken);
        var entity = await FirstOrDefaultAsync(value => value.Name == toolName, cancellationToken: cancellationToken)
            ?? throw new ToolStateUnavailableException();
        if (entity.ConcurrencyVersion != expected)
        {
            throw new BusinessException(
                TelemetryErrorConstants.TOOL_STATE_CONFLICT,
                TelemetryConstants.CONFLICT_MESSAGE,
                BusinessErrorKind.Conflict);
        }

        entity.IsEnabled = request.IsEnabled;
        entity.Title = request.Title ?? entity.Title;
        entity.Description = request.Description ?? entity.Description;
        try
        {
            // Native xmin still closes the race between the initial read and final write.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception) when (exception.Entries.Count == 1 && ReferenceEquals(exception.Entries[0].Entity, entity))
        {
            logger.LogWarning(TelemetryLogConstants.CONCURRENCY_REJECTED, nameof(SaveStateAsync));
            throw new BusinessException(
                TelemetryErrorConstants.TOOL_STATE_CONFLICT,
                TelemetryConstants.CONFLICT_MESSAGE,
                BusinessErrorKind.Conflict);
        }

        return entity;
    }
}
