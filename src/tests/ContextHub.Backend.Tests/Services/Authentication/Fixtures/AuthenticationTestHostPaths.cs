namespace ContextHub.Backend.Tests.Services.Authentication.Fixtures;

/// <summary>
/// Resolves the owning API source instead of sharing copied settings with the other referenced host.
/// </summary>
internal static class AuthenticationTestHostPaths
{
    /// <summary>
    /// Locates the Authentication content root from the test binary's checkout ancestors.
    /// </summary>
    /// <returns>The API directory containing its own environment settings and static assets.</returns>
    internal static string GetContentRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var contentRoot = Path.Combine(directory.FullName, "src", "services", "Authentication", "ContextHub.Authentication.Api");
            if (File.Exists(Path.Combine(contentRoot, "ContextHub.Authentication.Api.csproj")))
            {
                // Referenced hosts can overwrite each other's output settings; the source owner is unambiguous.
                return contentRoot;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The Authentication API source content root was not found.");
    }
}
