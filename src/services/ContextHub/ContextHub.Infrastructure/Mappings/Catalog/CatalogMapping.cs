namespace ContextHub.Infrastructure.Mappings.Catalog;

/// <summary>
/// Defines editable and public catalog mappings once through the existing Mapster global scan.
/// </summary>
public sealed class CatalogMapping : IRegister
{
    /// <summary>
    /// Maps only approved fields; source content and lifecycle transitions remain explicit operations.
    /// </summary>
    /// <param name="config">The process-wide startup mapping configuration.</param>
    public void Register(TypeAdapterConfig config)
    {
        // JsonObject owns its children; copy through the native DOM instead of mapping its collection implementation.
        config.NewConfig<JsonObject, JsonObject>()
            .MapWith(source => source.DeepClone().AsObject());

        config.NewConfig<CreatePackageRequestDto, Package>()
            .Map(destination => destination.Key, source => source.PackageId)
            .Map(destination => destination.Type, source => source.PackageType)
            .Map(destination => destination.Name, source => source.DisplayName.Trim())
            .Map(destination => destination.Summary, source => source.Summary.Trim())
            .Ignore(
                destination => destination.Id,
                destination => destination.CreatedAt,
                destination => destination.CreatedBy!,
                destination => destination.UpdatedAt,
                destination => destination.UpdatedBy!,
                destination => destination.ConcurrencyVersion,
                destination => destination.Versions);

        config.NewConfig<UpdatePackageRequestDto, Package>()
            .Map(destination => destination.Name, source => source.DisplayName.Trim())
            .Map(destination => destination.Summary, source => source.Summary.Trim())
            .Ignore(
                destination => destination.Id,
                destination => destination.Key,
                destination => destination.Type,
                destination => destination.CreatedAt,
                destination => destination.CreatedBy!,
                destination => destination.UpdatedAt,
                destination => destination.UpdatedBy!,
                destination => destination.ConcurrencyVersion,
                destination => destination.Versions);

        config.NewConfig<CatalogSearchRowModel, CatalogSearchItemResponseDto>()
            .Map(destination => destination.PackageType, source => source.Type)
            .Map(destination => destination.DisplayName, source => source.Name);
        config.NewConfig<Package, PackagePublicationStateModel>();

        config.NewConfig<PackageVersion, PackageVersionNumberModel>()
            .Map(destination => destination.PackageId, source => source.Package.Key);

        // Public revocations expose catalog keys rather than internal package UUIDs.
        config.NewConfig<PackageVersion, RevocationMetadataModel>()
            .Map(destination => destination.PackageId, source => source.Package.Key)
            .Map(destination => destination.Version, source => source.Number);

        config.NewConfig<PackageVersion, ImmutablePackageSourceModel>();

        config.NewConfig<PackageVersion, CatalogIndexEntryModel>()
            .Map(destination => destination.PackageId, source => source.Package.Key)
            .Map(destination => destination.PackageType, source => source.Package.Type)
            .Map(destination => destination.DisplayName, source => source.Package.Name)
            .Map(destination => destination.Summary, source => source.Package.Summary)
            .Map(destination => destination.Version, source => source.Number);

        config.NewConfig<PackageVersion, VersionSummaryResponseDto>()
            .Map(destination => destination.Version, source => source.Number);

        config.NewConfig<PackageVersion, VersionResponseDto>()
            .Map(destination => destination.PackageId, source => source.Package.Key)
            .Map(destination => destination.Version, source => source.Number)
            .Map(destination => destination.ConcurrencyToken, source => source.ConcurrencyVersion.ToString())
            .Map(
                destination => destination.Manifest,
                source => JsonNode.Parse(source.Manifest, null, default)!.AsObject())
            .Map(
                destination => destination.Bundle,
                source => JsonNode.Parse(source.Bundle, null, default)!.AsObject());
        config.NewConfig<Package, PackageResponseDto>()
            .Map(destination => destination.PackageId, source => source.Key)
            .Map(destination => destination.PackageType, source => source.Type)
            .Map(destination => destination.DisplayName, source => source.Name)
            .Map(destination => destination.ConcurrencyToken, source => source.ConcurrencyVersion.ToString())
            .Ignore(destination => destination.LatestVersion!);
        config.NewConfig<PackageVersion, PackageIdentityResponseDto>()
            .Map(value => value.PackageId, value => value.Package.Key)
            .Map(value => value.Version, value => value.Number);

        config.NewConfig<PackageVersion, PackageRevocationResponseDto>()
            .Map(value => value.PackageId, value => value.Package.Key)
            .Map(value => value.Version, value => value.Number);

        config.NewConfig<PackageVersion, PackageRequirementsResponseDto>()
            .Map(value => value.PackageId, value => value.Package.Key)
            .Map(value => value.Version, value => value.Number)
            .Ignore(value => value.RequiredCapabilities!);

        config.NewConfig<PackageVersion, PackageBundleResponseDto>()
            .Map(value => value.PackageId, value => value.Package.Key)
            .Map(value => value.Version, value => value.Number)
            .Map(value => value.PackageType, value => value.Package.Type)
            .Ignore(value => value.Bundle);

        config.NewConfig<PackageVersion, PackageMetadataResponseDto>()
            .Map(value => value.PackageId, value => value.Package.Key)
            .Map(value => value.Version, value => value.Number)
            .Map(value => value.PackageType, value => value.Package.Type)
            .Map(value => value.DisplayName, value => value.Package.Name)
            .Map(value => value.Summary, value => value.Package.Summary)
            .Ignore(value => value.Manifest)
            .Ignore(value => value.Applicability);

    }
}
