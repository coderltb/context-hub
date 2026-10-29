namespace ContextHub.Authentication.Infrastructure.Interceptors;

/// <summary>
/// Stamps Identity user timestamps and converts User removal into deactivation without altering other native stores.
/// </summary>
public sealed class UserSaveChangesInterceptor : SaveChangesInterceptor
{
    /// <summary>
    /// Applies timestamps before a synchronous native save.
    /// </summary>
    /// <param name="eventData">The context performing the save.</param>
    /// <param name="result">The current interception result.</param>
    /// <returns>The unchanged native save decision.</returns>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyUserChanges(eventData.Context);

        return result;
    }

    /// <summary>
    /// Applies the same timestamp semantics before an asynchronous save without taking ownership of completion.
    /// </summary>
    /// <param name="eventData">The context performing the save.</param>
    /// <param name="result">The current interception result.</param>
    /// <param name="cancellationToken">The save operation's cancellation token.</param>
    /// <returns>The unchanged native save decision.</returns>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyUserChanges(eventData.Context);

        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Keeps creation time immutable and derives all timestamps in one save from one UTC clock reading.
    /// </summary>
    /// <param name="context">The saving context, if the native event has one.</param>
    private static void ApplyUserChanges(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<User>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Deleted)
            {
                // Discard unrelated scalar edits before EF detects changes again after interception.
                entry.CurrentValues.SetValues(entry.OriginalValues);
                entry.State = EntityState.Unchanged;
                entry.Entity.IsActive = false;
                entry.Entity.UpdatedAt = now;
                entry.Entity.ConcurrencyStamp = Guid.NewGuid().ToString();

                // Explicit flags preserve the same narrow update when automatic change detection is disabled.
                entry.Property(value => value.IsActive).IsModified = true;
                entry.Property(value => value.UpdatedAt).IsModified = true;
                entry.Property(value => value.ConcurrencyStamp).IsModified = true;
            }
            else if (entry.State == EntityState.Modified)
            {
                // Detached updates must not overwrite the creation value already stored in PostgreSQL.
                entry.Property(value => value.CreatedAt).IsModified = false;
                entry.Entity.UpdatedAt = now;
                entry.Property(value => value.UpdatedAt).IsModified = true;
            }
        }
    }
}
