namespace ContextHub.Infrastructure.Mappings.Mcp;

/// <summary>
/// Maps configuration fields without granting requests control over identity, audit or xmin.
/// </summary>
public sealed class McpMapping : IRegister
{
    /// <summary>
    /// Registers global configuration projections and write allowlists once.
    /// </summary>
    /// <param name="config">The startup-owned global Mapster configuration.</param>
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Tool, ToolStateModel>();

        config.NewConfig<Tool, McpToolContractModel>();

        config.NewConfig<ToolStateModel, McpToolResponseDto>()
            .Map(
                value => value.ConcurrencyToken,
                value => PersistenceConcurrencyHelper.Encode(value.ConcurrencyVersion))
            .Ignore(value => value.Implementation!);

        config.NewConfig<CreateMcpPromptRequestDto, McpPrompt>()
            .Ignore(value => value.Id)
            .Ignore(value => value.CreatedAt)
            .Ignore(value => value.CreatedBy!)
            .Ignore(value => value.UpdatedAt)
            .Ignore(value => value.UpdatedBy!)
            .Ignore(value => value.ConcurrencyVersion)
            .Ignore(value => value.Arguments);

        config.NewConfig<UpdateMcpPromptRequestDto, McpPrompt>()
            .Ignore(value => value.Id)
            .Ignore(value => value.Name)
            .Ignore(value => value.CreatedAt)
            .Ignore(value => value.CreatedBy!)
            .Ignore(value => value.UpdatedAt)
            .Ignore(value => value.UpdatedBy!)
            .Ignore(value => value.ConcurrencyVersion)
            .Ignore(value => value.Arguments);

        config.NewConfig<McpPrompt, McpPromptSummaryResponseDto>()
            .Map(
                value => value.ConcurrencyToken,
                value => PersistenceConcurrencyHelper.Encode(value.ConcurrencyVersion));

        config.NewConfig<McpPrompt, McpPromptResponseDto>()
            .Map(
                value => value.ConcurrencyToken,
                value => PersistenceConcurrencyHelper.Encode(value.ConcurrencyVersion))
            .Ignore(value => value.Arguments);

        config.NewConfig<McpPrompt, McpPromptModel>()
            .Ignore(value => value.Arguments);
    }
}
