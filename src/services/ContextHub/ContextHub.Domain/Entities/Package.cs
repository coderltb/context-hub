namespace ContextHub.Domain.Entities;

/// <summary>
/// Owns stable public identity and editable catalog metadata.
/// </summary>
public sealed class Package : BaseEntity<Guid>
{
    /// <summary>
    /// Gets or sets the immutable public identifier.
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>
    /// Gets or sets the package kind from the public vocabulary.
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// Gets or sets the public English display name.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the public English search summary.
    /// </summary>
    public string Summary { get; set; } = "";

    /// <summary>
    /// Gets or sets whether consumers may discover the package.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the PostgreSQL optimistic concurrency version.
    /// </summary>
    public uint ConcurrencyVersion { get; set; }

    /// <summary>
    /// Gets or sets the immutable releases owned by this identity.
    /// </summary>
    public ICollection<PackageVersion> Versions { get; set; } = [];
}
