namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Keeps the local runtime schemas identical to the reviewed public contracts.
/// </summary>
public sealed class CatalogSchemaParityTests
{
    /// <summary>
    /// Checks package assets and both directions of every MCP tool against their documentation source.
    /// </summary>
    [Fact]
    public void EmbeddedSchemasMatchReviewedContracts()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "Documents", "reference")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        var packageAssembly = typeof(CatalogContentValidator).Assembly;
        var packageSchemas = packageAssembly.GetManifestResourceNames().Where(name => name.StartsWith("Schemas.", StringComparison.Ordinal)).ToArray();
        Assert.Equal(8, packageSchemas.Length);
        foreach (var name in packageSchemas)
        {
            using var stream = packageAssembly.GetManifestResourceStream(name)!;
            using var embedded = new StreamReader(stream);
            var reviewed = File.ReadAllText(Path.Combine(root.FullName, "Documents", "reference", "packages", name["Schemas.".Length..]));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(reviewed), JsonNode.Parse(embedded.ReadToEnd())), name);
        }

        var toolAssembly = typeof(ContextHub.Api.Configurations.Mcp.McpContractRegistry).Assembly;
        var schemas = toolAssembly.GetManifestResourceNames().Where(name => name.StartsWith("McpSchemas.", StringComparison.Ordinal)).ToArray();
        Assert.Empty(schemas);
        Assert.Equal(10, McpContractFixture.ReadContracts().Count);
    }
}
