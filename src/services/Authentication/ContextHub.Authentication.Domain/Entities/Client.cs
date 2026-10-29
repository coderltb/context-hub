namespace ContextHub.Authentication.Domain.Entities;

/// <summary>
/// Represents an application allowed to receive credentials independently of a human account.
/// </summary>
public sealed class Client : BaseEntity<Guid>
{
    /// <summary>
    /// Preserves client-owned UUID generation while the shared base remains independent of key strategy.
    /// </summary>
    public Client()
    {
        Id = Guid.CreateVersion7();
    }

    /// <summary>
    /// Gets or sets the administrator-supplied application label.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets whether Authentication accepts this application's credentials.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets PostgreSQL xmin for optimistic concurrency; it is not a business counter.
    /// </summary>
    public uint Version { get; set; }

    /// <summary>
    /// Gets or sets the independently revocable credentials owned by this application.
    /// </summary>
    public ICollection<ClientToken> Tokens { get; set; } = new List<ClientToken>();
}
