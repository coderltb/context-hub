namespace ContextHub.Infrastructure.Mappings.Integration;

/// <summary>
/// Registers data-only integration mappings in the existing global startup scan.
/// </summary>
public sealed class IntegrationMapping : IRegister
{
    /// <summary>
    /// Maps only editable inputs and approved response fields.
    /// </summary>
    /// <param name="config">The startup-owned global configuration.</param>
    public void Register(TypeAdapterConfig config)
    {
        // The ranking policy receives reviewed fields and vector identity, never a provider entity or URL.
        config.NewConfig<ExternalToolCandidateModel, ManagedToolRankingCandidateModel>()
            .Map(destination => destination.ConnectionId, source => source.Connection.Id)
            .Map(destination => destination.ToolName, source => source.Tool.Name)
            .Map(destination => destination.Title, source => source.Review.Title)
            .Map(destination => destination.Summary, source => source.Review.Summary)
            .Map(destination => destination.Embedding, source => source.Tool.Embedding)
            .Map(destination => destination.EmbeddingModel, source => source.Tool.EmbeddingModel);

        // Eligibility is evaluated against live policy before copying the preview fields.
        config.NewConfig<ExternalToolModel, ExternalToolResponseDto>()
            .Ignore(destination => destination.IsEligible);

        // Routing, ranking and schema are supplied only after current authority checks.
        config.NewConfig<ReviewedExternalToolOptions, ManagedToolCandidateResponseDto>()
            .Ignore(destination => destination.ToolRef)
            .Ignore(destination => destination.MatchReasons)
            .Ignore(destination => destination.InputOverview);

        config.NewConfig<ReviewedExternalToolOptions, ManagedToolDescriptionResponseDto>()
            .Map(destination => destination.Description, source => source.Summary)
            .Ignore(destination => destination.ToolRef)
            .Ignore(destination => destination.InputSchema)
            .Ignore(destination => destination.SideEffect);

        config.NewConfig<CreateRepositoryRequestDto, Repository>()
            .Map(destination => destination.Url, source => source.RepositoryUrl)
            .Map(destination => destination.DefaultRef, source => source.DefaultRef.Trim());

        config.NewConfig<UpdateRepositoryRequestDto, Repository>()
            .Map(destination => destination.Url, source => source.RepositoryUrl)
            .Map(destination => destination.DefaultRef, source => source.DefaultRef.Trim());

        config.NewConfig<Repository, RepositoryResponseDto>()
            .Map(destination => destination.RepositoryUrl, source => source.Url)
            .Map(destination => destination.ConcurrencyToken, source => source.ConcurrencyVersion.ToString());

        config.NewConfig<ExternalMcp, ExternalMcpResponseDto>()
            .Map(
                destination => destination.Config,
                source => JsonSerializer.Deserialize<ExternalMcpConfigDto>(source.Config, IntegrationJsonOptions.CANONICAL)!)
            .Map(destination => destination.ConcurrencyToken, source => source.ConcurrencyVersion.ToString());
    }
}
