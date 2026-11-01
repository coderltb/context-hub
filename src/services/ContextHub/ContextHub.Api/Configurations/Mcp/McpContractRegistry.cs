namespace ContextHub.Api.Configurations.Mcp;

/// <summary>
/// Compiles one request's database contract snapshot without fetching network references.
/// </summary>
public sealed class McpContractRegistry
{
    private readonly IReadOnlyDictionary<string, (JsonElement Input, JsonElement Output, JsonSchema InputSchema, JsonSchema OutputSchema)> _contracts;
    private readonly IReadOnlyDictionary<string, McpToolContractModel> _metadata;
    private readonly EvaluationOptions _evaluationOptions = new() { OutputFormat = OutputFormat.Flag };

    /// <summary>
    /// Compiles paired input and output schemas from an authoritative database read.
    /// </summary>
    /// <param name="contracts">The detached database contracts for this operation.</param>
    public McpContractRegistry(IReadOnlyList<McpToolContractModel> contracts)
    {
        _metadata = contracts.ToDictionary(contract => contract.Name, StringComparer.Ordinal);
        var buildOptions = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        buildOptions.SchemaRegistry.Fetch = (_, _) => null;
        _contracts = contracts.ToDictionary(
            contract => contract.Name,
            contract =>
        {
            using var input = JsonDocument.Parse(contract.InputSchema);
            using var output = JsonDocument.Parse(contract.OutputSchema);
            return (
                input.RootElement.Clone(),
                output.RootElement.Clone(),
                JsonSchema.FromText(contract.InputSchema, buildOptions),
                JsonSchema.FromText(contract.OutputSchema, buildOptions));
        },
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Gets a native tool definition using the same schemas enforced during invocation.
    /// </summary>
    /// <param name="name">The fixed public identifier.</param>
    /// <returns>The native SDK definition.</returns>
    public Tool GetDefinition(string name)
    {
        // Editable schemas and text come from the snapshot; executable tools and safety annotations stay code-owned.
        var metadata = ToolCatalogConstants.TOOLS.Single(tool => tool.Name == name);
        var contract = _contracts[name];

        return new Tool
        {
            Name = metadata.Name,
            Title = _metadata[name].Title,
            Description = _metadata[name].Description,
            InputSchema = contract.Input,
            OutputSchema = contract.Output,
            Annotations = new ToolAnnotations
            {
                ReadOnlyHint = metadata.ReadOnly,
                DestructiveHint = false,
                OpenWorldHint = false
            }
        };
    }

    /// <summary>
    /// Checks a submitted object against this request's current input schema.
    /// </summary>
    /// <param name="name">The selected allowlisted tool.</param>
    /// <param name="arguments">The submitted argument object.</param>
    /// <returns>Whether the complete input is valid.</returns>
    public bool IsValidInput(string name, JsonNode arguments) =>
        _contracts.TryGetValue(name, out var contract) && contract.InputSchema.Evaluate(JsonSerializer.SerializeToElement(arguments), _evaluationOptions).IsValid;

    /// <summary>
    /// Checks structured output before any result is returned to the client.
    /// </summary>
    /// <param name="name">The selected allowlisted tool.</param>
    /// <param name="result">The success or safe error object.</param>
    /// <returns>Whether the complete result matches the advertised contract.</returns>
    public bool IsValidOutput(string name, JsonNode result) =>
        _contracts.TryGetValue(name, out var contract) && contract.OutputSchema.Evaluate(JsonSerializer.SerializeToElement(result), _evaluationOptions).IsValid;

}
