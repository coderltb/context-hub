namespace ContextHub.Core.Persistence.Interceptors;

/// <summary>
/// Stamps only opt-in entities without coupling audit to Unit of Work or authentication.
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    /// <summary>
    /// Provides UTC time for this save boundary.
    /// </summary>
    private readonly TimeProvider _clock;

    /// <summary>
    /// Supplies the actor independently of its transport.
    /// </summary>
    private readonly IAuditActorAccessor _actor;

    /// <summary>
    /// Records audit counts without actor identifiers or property values.
    /// </summary>
    private readonly ILogger<AuditSaveChangesInterceptor> _logger;

    /// <summary>
    /// Creates a scoped interceptor from host-selected actor and clock.
    /// </summary>
    /// <param name="clock">The UTC time provider.</param>
    /// <param name="actor">The operation-local actor accessor.</param>
    /// <param name="logger">The safe audit metadata logger.</param>
    public AuditSaveChangesInterceptor(TimeProvider clock, IAuditActorAccessor actor, ILogger<AuditSaveChangesInterceptor> logger)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(logger);

        _clock = clock;
        _actor = actor;
        _logger = logger;
    }

    /// <summary>
    /// Stamps synchronous saves without owning the transaction.
    /// </summary>
    /// <param name="eventData">The context about to save.</param>
    /// <param name="result">The existing interception result.</param>
    /// <returns>The unchanged interception result.</returns>
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);

        return result;
    }

    /// <summary>
    /// Stamps asynchronous saves while preserving caller cancellation.
    /// </summary>
    /// <param name="eventData">The context about to save.</param>
    /// <param name="result">The existing interception result.</param>
    /// <param name="cancellationToken">The save cancellation token.</param>
    /// <returns>The unchanged interception result.</returns>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stamp(eventData.Context);

        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Applies one UTC save boundary to opt-in additions and modifications while protecting creation fields.
    /// </summary>
    /// <param name="context">The caller-owned context; absence means there is nothing to stamp.</param>
    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var entries = context.ChangeTracker.Entries<IAuditableEntity>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified).ToArray();
        if (entries.Length == 0)
        {
            return;
        }

        var actor = _actor.ActorId;
        var now = _clock.GetUtcNow().ToUniversalTime();

        // Validate every actor requirement first, preventing a partial stamp when a later entity rejects anonymous work.
        foreach (var entry in entries)
        {
            var requiresActor = !entry.Property(entity => entity.UpdatedBy).Metadata.IsNullable ||
                entry.State == EntityState.Added && !entry.Property(entity => entity.CreatedBy).Metadata.IsNullable;
            if (actor is null && requiresActor)
            {
                _logger.LogWarning(CoreLogConstants.AuditLogs.ACTOR_REQUIRED);

                throw new InvalidOperationException(CoreErrorConstants.AuditErrors.ACTOR_REQUIRED);
            }
        }

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = actor;
            }
            else
            {
                // Prevent creation columns from entering UPDATE even for detached entities. Tracked originals restore in-memory values.
                var createdAt = entry.Property(entity => entity.CreatedAt);
                var createdBy = entry.Property(entity => entity.CreatedBy);
                createdAt.CurrentValue = createdAt.OriginalValue;
                createdBy.CurrentValue = createdBy.OriginalValue;
                createdAt.IsModified = false;
                createdBy.IsModified = false;
            }

            entry.Entity.UpdatedAt = now;
            entry.Entity.UpdatedBy = actor;

            // Explicitly include audit changes even when the caller disables automatic change detection.
            if (entry.State == EntityState.Modified)
            {
                entry.Property(entity => entity.UpdatedAt).IsModified = true;
                entry.Property(entity => entity.UpdatedBy).IsModified = true;
            }
        }

        _logger.LogDebug(CoreLogConstants.AuditLogs.STAMPED, entries.Length);
    }
}
