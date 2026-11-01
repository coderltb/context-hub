namespace ContextHub.Application.Interfaces.Mcp;

/// <summary>
/// Exposes configuration I/O while Application owns prompt composition.
/// </summary>
public interface IMcpPromptService
{
    /// <summary>
    /// Reads a metadata page.
    /// </summary>
    /// <param name="request">The approved operation input.</param>
    /// <param name="cancellationToken">Cancels database work.</param>
    /// <returns>The detached configuration result.</returns>
    Task<PageResponse<McpPromptSummaryResponseDto>> GetPageAsync(
        GetMcpPromptsRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads one Owner configuration by database identifier.
    /// </summary>
    /// <param name="id">The approved operation input.</param>
    /// <param name="cancellationToken">Cancels database work.</param>
    /// <returns>The detached configuration result.</returns>
    Task<McpPromptResponseDto> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates configuration without persisting client input.
    /// </summary>
    /// <param name="request">The approved operation input.</param>
    /// <param name="cancellationToken">Cancels database work.</param>
    /// <returns>The detached configuration result.</returns>
    Task<McpPromptResponseDto> CreateAsync(CreateMcpPromptRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Updates mutable configuration with concurrency.
    /// </summary>
    /// <param name="id">The approved operation input.</param>
    /// <param name="request">The approved operation input.</param>
    /// <param name="cancellationToken">Cancels database work.</param>
    /// <returns>The detached configuration result.</returns>
    Task<McpPromptResponseDto> UpdateAsync(
        Guid id,
        UpdateMcpPromptRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads live enabled definitions.
    /// </summary>
    /// <param name="cancellationToken">Cancels database work.</param>
    /// <returns>The detached configuration result.</returns>
    Task<IReadOnlyList<McpPromptModel>> GetEnabledAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads one live enabled definition by MCP name.
    /// </summary>
    /// <param name="name">The approved operation input.</param>
    /// <param name="cancellationToken">Cancels database work.</param>
    /// <returns>The detached configuration result.</returns>
    Task<McpPromptModel?> FindEnabledAsync(string name, CancellationToken cancellationToken);

}
