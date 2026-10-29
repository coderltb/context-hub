namespace ContextHub.Core.Helpers.Persistence;

/// <summary>
/// Creates text command settings and borrows existing EF transactions without owning them.
/// </summary>
public static class DapperCommandOptionsHelper
{
    /// <summary>
    /// Creates the default text command with cancellation and optional transaction.
    /// </summary>
    /// <param name="cancellationToken">The command cancellation token.</param>
    /// <param name="transaction">The optional borrowed transaction.</param>
    /// <returns>The immutable command settings.</returns>
    public static DapperCommandOptions CreateText(CancellationToken cancellationToken = default, IDbTransaction? transaction = null) =>
        new() { CancellationToken = cancellationToken, Transaction = transaction };

    /// <summary>
    /// Requires an active EF transaction before borrowing its provider transaction.
    /// </summary>
    /// <param name="dbContext">The caller-owned EF context.</param>
    /// <param name="cancellationToken">The command cancellation token.</param>
    /// <returns>Text settings tied to the caller transaction.</returns>
    public static DapperCommandOptions CreateTransactionalText(DbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var transaction = dbContext.Database.CurrentTransaction ??
            throw new InvalidOperationException(CoreErrorConstants.DatabaseErrors.TRANSACTION_REQUIRED);

        return CreateText(cancellationToken, transaction.GetDbTransaction());
    }
}
