namespace ContextHub.Infrastructure.Services.Mcp;

/// <summary>
/// Persists prompt configuration while leaving request-local composition in Application.
/// </summary>
/// <param name="mcpPromptRepository">The scoped specific repository.</param>
/// <param name="unitOfWork">The matching save boundary.</param>
/// <param name="jsonSerializerService">The shared configuration serializer.</param>
/// <param name="logger">The safe concurrency-event logger.</param>
public sealed class McpPromptService(
    IMcpPromptRepository mcpPromptRepository,
    IUnitOfWork unitOfWork,
    IJsonSerializerService jsonSerializerService,
    ILogger<McpPromptService> logger) : IMcpPromptService
{
    /// <summary>
    /// Reads metadata without deserializing content or argument definitions.
    /// </summary>
    /// <param name="request">The validated list input.</param>
    /// <param name="cancellationToken">Cancels page queries.</param>
    /// <returns>The mapped metadata page.</returns>
    public Task<PageResponse<McpPromptSummaryResponseDto>> GetPageAsync(
        GetMcpPromptsRequestDto request,
        CancellationToken cancellationToken) =>
        mcpPromptRepository.GetPageAsync(request, cancellationToken);

    /// <summary>
    /// Reads one Owner configuration by its database identifier.
    /// </summary>
    /// <param name="id">The database identifier.</param>
    /// <param name="cancellationToken">Cancels the authority read.</param>
    /// <returns>The approved configuration.</returns>
    public async Task<McpPromptResponseDto> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var prompt = await mcpPromptRepository.FirstOrDefaultAsync<McpPrompt>(
            predicate: value => value.Id == id,
            cancellationToken: cancellationToken)
            ?? throw new BusinessException(
                McpPromptConstants.NOT_FOUND,
                McpPromptConstants.NOT_FOUND_MESSAGE,
                BusinessErrorKind.NotFound);

        return prompt.Adapt<McpPromptResponseDto>() with { Arguments = jsonSerializerService.DeserializeList<McpPromptArgumentDto>(prompt.Arguments) };
    }

    /// <summary>
    /// Stages only allowed configuration fields and commits once.
    /// </summary>
    /// <param name="request">The validated creation input.</param>
    /// <param name="cancellationToken">Cancels staging and save.</param>
    /// <returns>The committed Owner configuration.</returns>
    public async Task<McpPromptResponseDto> CreateAsync(
        CreateMcpPromptRequestDto request,
        CancellationToken cancellationToken)
    {
        var prompt = request.Adapt<McpPrompt>();
        var arguments = McpPromptArgumentPolicy.Merge([], request.Arguments, []);
        prompt.Arguments = jsonSerializerService.Serialize(arguments);
        await mcpPromptRepository.AddAsync(prompt, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: McpPromptConstants.NAME_CONSTRAINT })
        {
            // Only the known name constraint represents a public creation conflict.
            logger.LogWarning(TelemetryLogConstants.CONCURRENCY_REJECTED, nameof(CreateAsync));
            throw new BusinessException(
                McpPromptConstants.CONFLICT,
                McpPromptConstants.CONFLICT_MESSAGE,
                BusinessErrorKind.Conflict);
        }

        return prompt.Adapt<McpPromptResponseDto>() with { Arguments = arguments };
    }

    /// <summary>
    /// Preserves name and creation metadata while native xmin closes the update race.
    /// </summary>
    /// <param name="id">The database identifier selected by Owner.</param>
    /// <param name="request">The validated update and expected version.</param>
    /// <param name="cancellationToken">Cancels lookup and save.</param>
    /// <returns>The committed configuration and new version.</returns>
    public async Task<McpPromptResponseDto> UpdateAsync(
        Guid id,
        UpdateMcpPromptRequestDto request,
        CancellationToken cancellationToken)
    {
        var expected = PersistenceConcurrencyHelper.Decode(request.ConcurrencyToken);
        var prompt = await mcpPromptRepository.FirstOrDefaultAsync(
            value => value.Id == id,
            cancellationToken: cancellationToken)
            ?? throw new BusinessException(
                McpPromptConstants.NOT_FOUND,
                McpPromptConstants.NOT_FOUND_MESSAGE,
                BusinessErrorKind.NotFound);
        if (prompt.ConcurrencyVersion != expected)
        {
            throw new BusinessException(
                McpPromptConstants.CONFLICT,
                McpPromptConstants.CONFLICT_MESSAGE,
                BusinessErrorKind.Conflict);
        }

        var stored = jsonSerializerService.DeserializeList<McpPromptArgumentDto>(prompt.Arguments);
        var arguments = McpPromptArgumentPolicy.Merge(stored, request.Arguments, request.RemovedArgumentIds);
        request.Adapt(prompt);
        prompt.Arguments = jsonSerializerService.Serialize(arguments);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception) when (exception.Entries.Count == 1 && ReferenceEquals(
            exception.Entries[0].Entity,
            prompt))
        {
            logger.LogWarning(TelemetryLogConstants.CONCURRENCY_REJECTED, nameof(UpdateAsync));
            throw new BusinessException(
                McpPromptConstants.CONFLICT,
                McpPromptConstants.CONFLICT_MESSAGE,
                BusinessErrorKind.Conflict);
        }

        return prompt.Adapt<McpPromptResponseDto>() with { Arguments = arguments };
    }

    /// <summary>
    /// Reads enabled configurations in stable name order without retaining tracked entities.
    /// </summary>
    /// <param name="cancellationToken">Cancels the live query.</param>
    /// <returns>Detached definitions for native discovery.</returns>
    public async Task<IReadOnlyList<McpPromptModel>> GetEnabledAsync(CancellationToken cancellationToken)
    {
        var prompts = await mcpPromptRepository.GetListAsync<McpPrompt>(
            predicate: value => value.IsEnabled,
            order: QueryOrder<McpPrompt>.By(value => value.Name),
            cancellationToken: cancellationToken);
        return prompts.Select(prompt => prompt.Adapt<McpPromptModel>() with
        {
            Arguments = jsonSerializerService.DeserializeList<McpPromptArgumentDto>(prompt.Arguments).Where(value => !value.IsDeleted).ToArray()
        }).ToArray();
    }

    /// <summary>
    /// Reads one enabled configuration snapshot by the native MCP identifier.
    /// </summary>
    /// <param name="name">The immutable MCP name.</param>
    /// <param name="cancellationToken">Cancels the authority read.</param>
    /// <returns>The definition or null when missing or disabled.</returns>
    public async Task<McpPromptModel?> FindEnabledAsync(
        string name,
        CancellationToken cancellationToken)
    {
        var prompt = await mcpPromptRepository.FirstOrDefaultAsync<McpPrompt>(
            predicate: value => value.Name == name && value.IsEnabled,
            cancellationToken: cancellationToken);
        return prompt is null ? null : prompt.Adapt<McpPromptModel>() with
        {
            Arguments = jsonSerializerService.DeserializeList<McpPromptArgumentDto>(prompt.Arguments).Where(value => !value.IsDeleted).ToArray()
        };
    }

}
