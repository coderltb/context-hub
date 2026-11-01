namespace ContextHub.Infrastructure.Services.Integration;

/// <summary>
/// Keeps Owner connection CRUD separate from public managed-tool payloads.
/// </summary>
/// <param name="connectionRepository">The specific connection repository.</param>
/// <param name="toolRegistry">The scoped reviewed metadata registry.</param>
/// <param name="cacheService">The derived-snapshot cache.</param>
public sealed class ExternalMcpService(
    IExternalConnectionRepository connectionRepository,
    IExternalToolRegistry toolRegistry,
    ICacheService cacheService) : IExternalMcpService
{
    /// <summary>
    /// Normalizes imports and performs preview-only native discovery.
    /// </summary>
    /// <param name="import">The supported bounded import document.</param>
    /// <param name="cancellationToken">Cancels native discovery.</param>
    /// <returns>Canonical items and Owner-only definitions.</returns>
    public async Task<ExternalMcpPreviewResponseDto> PreviewAsync(
        JsonElement import,
        CancellationToken cancellationToken)
    {
        var normalized = ExternalMcpImportHelper.Normalize(import);
        var items = new List<ExternalMcpPreviewItemDto>();
        foreach (var item in normalized)
        {
            var tools = await toolRegistry.PreviewAsync(item.Name, item.Config, cancellationToken);
            items.Add(new() {
                Name = item.Name,
                Config = item.Config,
                Warnings = item.Warnings,
                Tools = tools
            });
        }
        return new() { Items = items };
    }

    /// <summary>
    /// Lists saved metadata without calling providers or exposing public routing references.
    /// </summary>
    /// <param name="cancellationToken">Cancels current state and snapshot reads.</param>
    /// <returns>The Owner-only connection list.</returns>
    public async Task<ExternalMcpListResponseDto> GetListAsync(CancellationToken cancellationToken)
    {
        var connections = await connectionRepository.GetConnectionsAsync(cancellationToken);
        var result = new List<ExternalMcpResponseDto>();
        foreach (var connection in connections)
        {
            var value = connection.Adapt<ExternalMcpResponseDto>();
            var summary = await toolRegistry.GetSummaryAsync(connection, cancellationToken);
            result.Add(value with { Discovery = summary });
        }
        return new() { Items = result };
    }

    /// <summary>
    /// Saves only closed canonical configuration before invalidating rebuildable metadata.
    /// </summary>
    /// <param name="name">The Owner-only connection label.</param>
    /// <param name="request">The raw canonical object and expected version.</param>
    /// <param name="cancellationToken">Cancels endpoint checks, persistence and invalidation.</param>
    /// <returns>The committed metadata.</returns>
    public async Task<ExternalMcpResponseDto> SaveAsync(
        string name,
        SaveExternalMcpRequestDto request,
        CancellationToken cancellationToken)
    {
        var config = ExternalMcpImportHelper.ReadCanonical(request.Config);
        await ValidateDnsAsync(config.Url, cancellationToken);
        var current = await connectionRepository.GetByNameAsync(name, cancellationToken);
        uint? expected = null;
        if (current is null)
        {
            if (request.ConcurrencyToken is not null)
            {
                throw new BusinessException(IntegrationConstants.CONFLICT, IntegrationConstants.CONFLICT_MESSAGE, BusinessErrorKind.Conflict);
            }
        }
        else
        {
            if (!uint.TryParse(request.ConcurrencyToken, NumberStyles.None, CultureInfo.InvariantCulture, out var version))
            {
                throw new BusinessException(IntegrationConstants.CONFLICT, IntegrationConstants.CONFLICT_MESSAGE, BusinessErrorKind.Conflict);
            }

            expected = version;
        }
        current ??= new() {
            Id = Guid.CreateVersion7(),
            Name = name
        };
        current.Config = JsonSerializer.Serialize(config, IntegrationJsonOptions.CANONICAL);
        var saved = await connectionRepository.SaveAsync(current, expected, cancellationToken);

        // Existing routes also bind this configuration hash and are rejected even before cache invalidation propagates.
        await cacheService.RemoveAsync(ExternalToolRegistry.SnapshotKey(saved.Id), cancellationToken);
        return saved.Adapt<ExternalMcpResponseDto>();
    }

    /// <summary>
    /// Deletes current authority before invalidating metadata snapshots.
    /// </summary>
    /// <param name="name">The exact saved label.</param>
    /// <param name="concurrencyToken">The opaque If-Match version.</param>
    /// <param name="cancellationToken">Cancels persistence and cache invalidation.</param>
    /// <returns>The completed deletion result.</returns>
    public async Task<DeletedIntegrationResponseDto> DeleteAsync(
        string name,
        string concurrencyToken,
        CancellationToken cancellationToken)
    {
        var current = await connectionRepository.GetByNameAsync(name, cancellationToken)
            ?? throw new BusinessException(IntegrationConstants.NOT_FOUND, IntegrationConstants.MISSING_MESSAGE, BusinessErrorKind.NotFound);
        var expected = uint.Parse(concurrencyToken.Trim('"'), CultureInfo.InvariantCulture);
        await connectionRepository.DeleteAsync(current, expected, cancellationToken);
        await cacheService.RemoveAsync(ExternalToolRegistry.SnapshotKey(current.Id), cancellationToken);
        return new() { Deleted = true };
    }

    /// <summary>
    /// Rejects non-public saved destinations before they can become active connection metadata.
    /// </summary>
    /// <param name="url">The canonical endpoint.</param>
    /// <param name="cancellationToken">Cancels DNS resolution.</param>
    /// <returns>The completed endpoint validation.</returns>
    private static async Task ValidateDnsAsync(
        string url,
        CancellationToken cancellationToken)
    {
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(new Uri(url).Host, cancellationToken);
        }
        catch (SocketException exception)
        {
            throw new DependencyUnavailableException(IntegrationInfrastructureConstants.ENDPOINT_UNAVAILABLE, exception);
        }
        if (addresses.Length == 0 || addresses.Any(address => !ExternalEndpointHelper.IsPublicAddress(address)))
        {
            throw new BusinessException(IntegrationErrorConstants.UNSAFE_ENDPOINT, IntegrationConstants.INVALID_MESSAGE);
        }
    }
}
