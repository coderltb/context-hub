namespace ContextHub.Infrastructure.Services.Integration;

/// <summary>
/// Executes bounded hosted protocol calls through the native SDK.
/// </summary>
/// <param name="httpClientFactory">The factory owning checked native transports.</param>
public sealed class ExternalMcpClient(IHttpClientFactory httpClientFactory) : IExternalMcpClient
{
    /// <summary>
    /// Lists one bounded definition page without accepting silent upstream expansion.
    /// </summary>
    /// <param name="endpoint">The canonical HTTPS endpoint.</param>
    /// <param name="cancellationToken">Cancels protocol and content reads.</param>
    /// <returns>The private upstream definitions.</returns>
    public Task<IReadOnlyList<ExternalToolModel>> DiscoverAsync(string endpoint, CancellationToken cancellationToken) =>
        ExecuteAsync<IReadOnlyList<ExternalToolModel>>(
            endpoint,
            async client =>
        {
            var page = await client.ListToolsAsync(new ListToolsRequestParams(), cancellationToken);
            if (page.NextCursor is not null || page.Tools.Count > IntegrationInfrastructureConstants.MAX_DISCOVERED_TOOLS)
            {
                throw new DependencyUnavailableException(IntegrationErrorConstants.EXTERNAL_MCP_RESPONSE_INVALID);
            }
            return page.Tools.Select(tool =>
            {
                var definition = JsonSerializer.SerializeToElement(tool, McpJsonUtilities.DefaultOptions);
                var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(definition.GetRawText())));
                var readOnly = tool.Annotations?.ReadOnlyHint == true && tool.Annotations?.DestructiveHint != true;
                return new ExternalToolModel(tool.Name, tool.Title, tool.Description, tool.InputSchema.Clone(), hash, readOnly)
                {
                    OutputSchema = tool.OutputSchema?.Clone()
                };
            }).ToArray();
        },
            cancellationToken);

    /// <summary>
    /// Calls one exact reviewed operation without automatic replay.
    /// </summary>
    /// <param name="endpoint">The canonical HTTPS endpoint.</param>
    /// <param name="toolName">The reviewed upstream name.</param>
    /// <param name="arguments">The exact runtime-validated argument object.</param>
    /// <param name="cancellationToken">Cancels the native call.</param>
    /// <returns>The bounded native result.</returns>
    public Task<JsonElement> CallAsync(string endpoint, string toolName, JsonElement arguments, CancellationToken cancellationToken) =>
        ExecuteAsync(
            endpoint,
            async client =>
        {
            var values = arguments.EnumerateObject().ToDictionary(value => value.Name, value => (object?)value.Value.Clone(), StringComparer.Ordinal);
            var result = await client.CallToolAsync(toolName, values, cancellationToken: cancellationToken);
            return JsonSerializer.SerializeToElement(result, McpJsonUtilities.DefaultOptions);
        },
            cancellationToken);

    /// <summary>
    /// Owns one native client lifecycle and translates only recognized protocol/transport failures.
    /// </summary>
    /// <typeparam name="TResult">The bounded operation result.</typeparam>
    /// <param name="endpoint">The checked hosted endpoint.</param>
    /// <param name="action">The one native read operation.</param>
    /// <param name="cancellationToken">Cancels connection and native dispatch.</param>
    /// <returns>The completed native result.</returns>
    private async Task<TResult> ExecuteAsync<TResult>(
        string endpoint,
        Func<McpClient, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var transport = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = new Uri(ExternalMcpImportHelper.ValidateEndpoint(endpoint)),
                    TransportMode = HttpTransportMode.StreamableHttp,
                    ConnectionTimeout = TimeSpan.FromSeconds(10),
                    EnableStandaloneGetStream = false,
                    OwnsSession = false,
                    MaxReconnectionAttempts = 0
                },
                httpClientFactory.CreateClient(IntegrationInfrastructureConstants.EXTERNAL_HTTP_CLIENT),
                ownsHttpClient: true);
            await using var client = await McpClient.CreateAsync(
                transport,
                new McpClientOptions
                {
                    ProtocolVersion = IntegrationInfrastructureConstants.MCP_PROTOCOL,
                    ClientInfo = new()
                    {
                        Name = IntegrationInfrastructureConstants.MCP_CLIENT_NAME,
                        Version = IntegrationInfrastructureConstants.MCP_CLIENT_VERSION
                    }
                },
                cancellationToken: cancellationToken);
            return await action(client);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or McpException or JsonException or TimeoutException or TimeoutRejectedException or BrokenCircuitException)
        {
            throw new DependencyUnavailableException(IntegrationErrorConstants.EXTERNAL_MCP_UNAVAILABLE, exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DependencyUnavailableException(IntegrationErrorConstants.EXTERNAL_MCP_UNAVAILABLE, exception);
        }
    }
}
