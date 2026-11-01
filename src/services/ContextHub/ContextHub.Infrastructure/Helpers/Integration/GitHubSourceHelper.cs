namespace ContextHub.Infrastructure.Helpers.Integration;

/// <summary>
/// Validates persisted GitHub locations before constructing service-owned API routes.
/// </summary>
public static class GitHubSourceHelper
{
    /// <summary>
    /// Canonicalizes an exact GitHub repository URL without accepting credentials or API endpoints.
    /// </summary>
    /// <param name="value">The public source URL supplied by an Owner.</param>
    /// <returns>The canonical HTTPS GitHub repository URL.</returns>
    public static string CanonicalRepositoryUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.IsDefaultPort || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
        {
            throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
        }
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length != 2 || segments.Any(segment => segment is "" or "." or ".." ||
            segment.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('_' or '.' or '-'))))
        {
            throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
        }
        var repository = segments[1].EndsWith(".git", StringComparison.Ordinal) ? segments[1][..^4] : segments[1];
        if (repository.Length == 0)
        {
            throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
        }
        return $"https://github.com/{segments[0].ToLowerInvariant()}/{repository.ToLowerInvariant()}";
    }

    /// <summary>
    /// Encodes a relative source directory as path segments after traversal checks.
    /// </summary>
    /// <param name="path">The requested relative source directory.</param>
    /// <returns>The encoded relative path, or empty for the repository root.</returns>
    public static string EncodeSourcePath(string path)
    {
        if (path.Length > 1024 || path.StartsWith('/') || path.Contains('\\') || path.Any(char.IsControl))
        {
            throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
        }
        if (path.Length == 0 || path == ".")
        {
            return "";
        }
        var segments = path.TrimEnd('/').Split('/');
        if (segments.Any(segment => segment is "" or "." or ".." || segment.Equals(".git", StringComparison.OrdinalIgnoreCase)))
        {
            throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
        }
        return string.Join('/', segments.Select(Uri.EscapeDataString)) + "/";
    }
}
