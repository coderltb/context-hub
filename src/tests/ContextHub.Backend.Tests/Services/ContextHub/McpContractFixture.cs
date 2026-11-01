using ContextHub.Api.Configurations.Mcp;
using ContextHub.Application.Constants.Telemetry;
using ContextHub.Application.Constants.Mcp;
using ContextHub.Application.Models.Mcp;
using System.Text.RegularExpressions;

namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Loads reviewed documentation contracts for isolated tests without restoring a production file fallback.
/// </summary>
internal static class McpContractFixture
{
    /// <summary>
    /// Reads the two reviewed schemas for each explicitly registered tool.
    /// </summary>
    /// <returns>The synthetic authoritative contracts used by test ports.</returns>
    public static IReadOnlyList<McpToolContractModel> ReadContracts()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "Documents", "reference")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        return ToolCatalogConstants.TOOLS.Select(tool =>
        {
            var text = File.ReadAllText(Path.Combine(root.FullName, "Documents", "reference", "api", "v1", "mcp", "tools",
                tool.Name.Replace('_', '-') + ".md"));
            var schemas = Regex.Matches(text, @"```json\s*(.*?)\s*```", RegexOptions.Singleline)
                .Select(match => JsonNode.Parse(match.Groups[1].Value))
                .OfType<JsonObject>().Where(schema => schema.ContainsKey("$schema")).ToArray();
            Assert.Equal(2, schemas.Length);

            return new McpToolContractModel(tool.Name, true, schemas[0].ToJsonString(), schemas[1].ToJsonString());
        }).ToArray();
    }

    /// <summary>
    /// Creates a request-local evaluator for existing response contract assertions.
    /// </summary>
    /// <returns>The compiled reviewed test contracts.</returns>
    public static McpContractRegistry CreateRegistry() => new(ReadContracts());
}
