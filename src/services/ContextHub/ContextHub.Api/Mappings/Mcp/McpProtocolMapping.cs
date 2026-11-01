namespace ContextHub.Api.Mappings.Mcp;

/// <summary>
/// Maps approved discovery snapshots to native SDK types after the adapter has checked current authority.
/// </summary>
public sealed class McpProtocolMapping : IRegister
{
    /// <summary>
    /// Registers data-only discovery mappings in the existing process-wide startup scan.
    /// </summary>
    /// <param name="config">The startup-owned global Mapster configuration.</param>
    public void Register(TypeAdapterConfig config)
    {
        // Optional argument metadata retains its nullable native protocol representation.
        config.NewConfig<McpPromptArgumentDto, PromptArgument>()
            .Map(destination => destination.Name, source => source.Name)
            .Map(destination => destination.Description, source => source.Description)
            .Map(destination => destination.Required, source => source.Required)
            .IgnoreNonMapped(true);

        // Discovery exposes this allowlist, never stored content or future fields added to the internal snapshot.
        config.NewConfig<McpPromptModel, Prompt>()
            .Map(destination => destination.Name, source => source.Name)
            .Map(destination => destination.Title, source => source.Title)
            .Map(destination => destination.Description, source => source.Description)
            .Map(destination => destination.Arguments, source => source.Arguments)
            .IgnoreNonMapped(true);

        // Fixed routing and MIME metadata are published only after the live grant filter.
        config.NewConfig<McpResourceDefinitionModel, Resource>()
            .Map(destination => destination.Name, source => source.Name)
            .Map(destination => destination.Uri, source => source.Uri)
            .Map(destination => destination.MimeType, _ => McpResourceConstants.MIME_TYPE)
            .IgnoreNonMapped(true);

        config.NewConfig<McpResourceDefinitionModel, ResourceTemplate>()
            .Map(destination => destination.Name, source => source.Name)
            .Map(destination => destination.UriTemplate, source => source.Uri)
            .Map(destination => destination.MimeType, _ => McpResourceConstants.MIME_TYPE)
            .IgnoreNonMapped(true);
    }
}
