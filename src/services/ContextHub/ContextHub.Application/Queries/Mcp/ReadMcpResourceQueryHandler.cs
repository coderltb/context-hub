namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Rechecks global enablement so a cached resource URI cannot bypass a disabled capability.
/// </summary>
/// <param name="catalogService">The live catalog and immutable-content boundary.</param>
/// <param name="toolStateService">The current global tool authority.</param>
public sealed class ReadMcpResourceQueryHandler(
    ICatalogService catalogService,
    IToolStateService toolStateService)
    : IRequestHandler<ReadMcpResourceQuery, JsonNode>
{
    /// <summary>
    /// Reads only the approved public projection for the selected route.
    /// </summary>
    /// <param name="request">The authorized resource request.</param>
    /// <param name="cancellationToken">Cancels authoritative reads.</param>
    /// <returns>The resource's JSON content.</returns>
    public async ValueTask<JsonNode> Handle(
        ReadMcpResourceQuery request,
        CancellationToken cancellationToken)
    {
        if (!await toolStateService.IsEnabledAsync(request.ToolName, cancellationToken))
        {
            throw new CoreAuthorizationException();
        }
        var resource = request.Resource;
        var lookup = new PackageLookupRequestDto {
            PackageId = resource.PackageId ?? "",
            Version = resource.Version
        };
        return resource.Kind switch
        {
            McpResourceKind.Index => await catalogService.GetCatalogIndexAsync(cancellationToken),
            McpResourceKind.Revocations => await catalogService.GetRevocationsIndexAsync(cancellationToken),
            McpResourceKind.Content => await catalogService.GetPublicContentAsync(lookup, cancellationToken),
            McpResourceKind.Requirements => await catalogService.GetRequirementsAsync(lookup, cancellationToken),
            McpResourceKind.Manifest => (await catalogService.GetPublicPackageAsync(lookup, cancellationToken))["manifest"]!.DeepClone().AsObject(),
            _ => throw new CoreAuthorizationException()
        };
    }
}
