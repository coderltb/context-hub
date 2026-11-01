namespace ContextHub.Application.Models.Mcp;

/// <summary>
/// Describes one of the five fixed public resource routes without SDK or HTTP dependencies.
/// </summary>
/// <param name="Kind">The exact resource kind.</param>
/// <param name="ToolName">The associated capability grant.</param>
/// <param name="PackageId">The public package identifier when applicable.</param>
/// <param name="Version">The exact version when applicable.</param>
public sealed record McpResourceRequestModel(
    McpResourceKind Kind,
    string ToolName,
    string? PackageId = null,
    string? Version = null)
{
    /// <summary>
    /// Recognizes only the fixed local resource scheme and route shapes.
    /// </summary>
    /// <param name="value">The consumer-supplied URI.</param>
    /// <param name="resource">The recognized resource or null.</param>
    /// <returns>Whether the route belongs to the public surface.</returns>
    public static bool TryParse(
        string value,
        out McpResourceRequestModel? resource)
    {
        resource = null;
        if (value.Length > 2048 || !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme != "contexthub" || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
        {
            return false;
        }

        var fixedRoute = McpResourceConstants.DEFINITIONS.FirstOrDefault(definition => definition.Leaf is null &&
            (definition.Kind != McpResourceKind.Revocations || uri.AbsolutePath is "" or "/") &&
            definition.Uri == (definition.Kind == McpResourceKind.Revocations ? uri.AbsoluteUri.TrimEnd('/') : uri.AbsoluteUri));
        if (fixedRoute is not null)
        {
            resource = new(fixedRoute.Kind, fixedRoute.ToolName);
            return true;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (uri.Host != "packages" || segments.Length != 3)
        {
            return false;
        }
        var definition = McpResourceConstants.DEFINITIONS.FirstOrDefault(candidate => candidate.Leaf == segments[2]);
        if (definition is null)
        {
            return false;
        }
        resource = new(
            definition.Kind,
            definition.ToolName,
            Uri.UnescapeDataString(segments[0]),
            Uri.UnescapeDataString(segments[1]));
        return true;
    }
}
