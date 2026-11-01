namespace ContextHub.Infrastructure.Helpers.Integration;

/// <summary>
/// Projects the reviewed documentation provider's text format into bounded neutral fields.
/// </summary>
public static class Context7DocumentationHelper
{
    /// <summary>
    /// Selects a ranked library and accepts only a requested version explicitly advertised by it.
    /// </summary>
    /// <param name="text">The successful bounded resolver text.</param>
    /// <param name="library">The public library name.</param>
    /// <param name="version">The optional requested documentation version.</param>
    /// <returns>The selected private identifier and neutral name, or null.</returns>
    public static (string Id, string Name)? SelectLibrary(
        string text,
        string library,
        string? version)
    {
        var matches = Regex.Matches(
            text,
            @"- Title: (?<name>[^\r\n]+)\r?\n- Context7-compatible library ID: (?<id>/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+)(?<rest>.*?)(?=\r?\n----------|\z)",
            RegexOptions.Singleline | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        var candidates = new List<(string Id, string Name)>();
        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value;
            if (name.Length is < 1 or > 256)
            {
                continue;
            }
            var id = match.Groups["id"].Value;
            if (version is not null)
            {
                var versions = Regex.Match(
                    match.Groups["rest"].Value,
                    @"- Versions: (?<versions>[^\r\n]+)",
                    RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(100));
                var advertised = versions.Groups["versions"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(value => value.Equals(version, StringComparison.Ordinal) || value.Equals("v" + version, StringComparison.Ordinal));
                if (advertised is null)
                {
                    continue;
                }
                id += "/" + advertised;
            }
            candidates.Add((id, name));
        }
        // Preserve provider rank after removing candidates that cannot satisfy the requested version.
        var exact = candidates.FindIndex(value => value.Name.Equals(library, StringComparison.OrdinalIgnoreCase));
        if (exact >= 0)
        {
            return candidates[exact];
        }
        return candidates.Count > 0 ? candidates[0] : null;
    }

    /// <summary>
    /// Extracts only bounded public source excerpts from the reviewed text layout.
    /// </summary>
    /// <param name="text">The successful bounded documentation text.</param>
    /// <returns>At most eight neutral documentation excerpts.</returns>
    public static IReadOnlyList<LibraryDocumentationDocumentResponseDto> ReadDocuments(string text)
    {
        var matches = Regex.Matches(
            text,
            @"(?:^|\n)#{1,6} (?<title>[^\r\n]+)\r?\n+(?:Source|SOURCE):\s*(?<url>https://[^\s]+)\r?\n(?<excerpt>.*?)(?=\n#{1,6} |\z)",
            RegexOptions.Singleline | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));
        var documents = new List<LibraryDocumentationDocumentResponseDto>();
        foreach (Match match in matches.Take(8))
        {
            var title = match.Groups["title"].Value.Trim();
            var url = match.Groups["url"].Value.Trim();
            var excerpt = match.Groups["excerpt"].Value.Trim();
            if (title.Length is < 1 or > 256 || excerpt.Length is < 1 or > 8192 || url.Length > 2048 ||
                !Uri.TryCreate(url, UriKind.Absolute, out var source) || source.Scheme != Uri.UriSchemeHttps || source.UserInfo.Length != 0 || source.Query.Length != 0)
            {
                continue;
            }
            documents.Add(new(title, source.AbsoluteUri, excerpt));
        }
        return documents;
    }
}
