namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Mappings;

/// <summary>
/// Supplies stable test mappings through the same parameterless profile mechanism used by application features.
/// </summary>
public sealed class GlobalMappingProfile : IRegister
{
    /// <summary>
    /// Declares aliases without capturing a container, request or mutable test state.
    /// </summary>
    /// <param name="config">The startup configuration receiving this profile.</param>
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<GlobalMappingSource, GlobalMappingTarget>()
            .Map(destination => destination.DisplayName, source => source.Name);
        config.NewConfig<TestEntity, GlobalMappingTarget>()
            .Map(destination => destination.DisplayName, source => source.Name);
    }
}

/// <summary>
/// Represents an ordinary feature input discovered from its loaded assembly.
/// </summary>
public sealed class GlobalMappingSource
{
    /// <summary>
    /// Gets the source name without tying the mapping to any DI scope.
    /// </summary>
    public string Name { get; init; } = string.Empty;
}

/// <summary>
/// Represents the aliased output used to verify discovery and HTTP serialization.
/// </summary>
public sealed class GlobalMappingTarget
{
    /// <summary>
    /// Gets or sets the renamed destination member supplied by the profile.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
