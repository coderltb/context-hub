namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Composes request-local guidance while tool grants remain independently enforced.
/// </summary>
/// <param name="mcpPromptService">The live configuration reader.</param>
/// <param name="mediator">The existing grant-aware capabilities query dispatcher.</param>
public sealed class RenderMcpPromptQueryHandler(
    IMcpPromptService mcpPromptService,
    IMediator mediator)
    : IRequestHandler<RenderMcpPromptQuery, McpPromptMessageModel>
{
    /// <summary>
    /// Checks arguments against the same configuration snapshot used for composition.
    /// </summary>
    /// <param name="request">The transient name and argument values.</param>
    /// <param name="cancellationToken">Cancels authority reads.</param>
    /// <returns>A transient message that is never stored or logged.</returns>
    public async ValueTask<McpPromptMessageModel> Handle(
        RenderMcpPromptQuery request,
        CancellationToken cancellationToken)
    {
        var prompt = await mcpPromptService.FindEnabledAsync(request.Name, cancellationToken)
            ?? throw new BusinessException(McpPromptConstants.NOT_FOUND, McpPromptConstants.NOT_FOUND_MESSAGE, BusinessErrorKind.NotFound);

        // Stored definitions are trusted configuration; transient consumer values still need type and name checks.
        var arguments = request.Arguments;
        var invalidRequired = prompt.Arguments.Any(argument => argument.Required == true &&
            (arguments is null || !arguments.TryGetValue(argument.Name, out var value) ||
                value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())));
        var invalidSupplied = arguments?.Any(pair => pair.Value.ValueKind != JsonValueKind.String ||
            pair.Value.GetString()!.Length > McpPromptConstants.MAX_ARGUMENT_LENGTH ||
            !prompt.Arguments.Any(argument => argument.Name == pair.Key)) == true;
        if (invalidRequired || invalidSupplied)
        {
            throw new BusinessException(McpPromptConstants.ARGUMENTS_INVALID, McpPromptConstants.ARGUMENTS_INVALID_MESSAGE);
        }

        // Live grants constrain guidance even when a client retained an earlier prompt definition.
        var available = await mediator.Send(new GetMcpDiscoveryQuery(), cancellationToken);
        var text = prompt.Content + McpPromptConstants.AVAILABLE_CAPABILITIES_PREFIX + string.Join(", ", available) +
            McpPromptConstants.CAPABILITY_GUIDANCE;

        // Arguments affect this returned message only; they never become saved configuration.
        if (arguments is not null)
        {
            text += McpPromptConstants.REQUESTED_INPUTS_PREFIX + JsonSerializer.Serialize(arguments);
        }

        return new(prompt.Content, text);
    }
}
