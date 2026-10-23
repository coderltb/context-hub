namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Mappings;

/// <summary>
/// Defines stable PostgreSQL projection fixtures in a mapping file, independent of individual tests and DI scopes.
/// </summary>
public sealed class RepositoryMappingProfile : IRegister
{
    /// <summary>
    /// Renames parent and navigation fields while leaving EF responsible for translating the resulting projection.
    /// </summary>
    /// <param name="config">The process-wide startup configuration receiving these mappings.</param>
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<NavigationCategory, RepositoryMappingTests.CategoryDto>()
            .Map(destination => destination.Title, source => source.Name);
        config.NewConfig<NavigationVariant, RepositoryMappingTests.VariantDto>()
            .Map(destination => destination.Label, source => source.Name);

        // Navigation projection does not load full entities or require Include; nullable objects remain nullable.
        config.NewConfig<NavigationProduct, RepositoryMappingTests.ProductDto>()
            .Map(destination => destination.Code, source => source.Id)
            .Map(destination => destination.DisplayName, source => source.Name)
            .Map(destination => destination.Group, source => source.Category)
            .Map(destination => destination.Options, source => source.Variants);
        config.NewConfig<NavigationProduct, RepositoryMappingTests.NameDto>()
            .Map(destination => destination.DisplayName, source => source.Name);
    }
}
