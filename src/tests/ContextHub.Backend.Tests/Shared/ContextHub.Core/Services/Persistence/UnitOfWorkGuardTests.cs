namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Services.Persistence;

/// <summary>
/// Rejects invalid callbacks before a transaction touches an unconfigured database context.
/// </summary>
public sealed class UnitOfWorkGuardTests
{
    /// <summary>
    /// Both callback forms report the invalid argument even when database access would fail.
    /// </summary>
    /// <param name="returnsValue">Whether the generic transaction overload is selected.</param>
    /// <param name="explicitIsolation">Whether the caller requests a particular native isolation level.</param>
    /// <returns>The asynchronous guard assertions without opening a connection.</returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Transaction_NullCallback_IsRejectedBeforeDatabaseAccess(bool returnsValue, bool explicitIsolation)
    {
        // With no provider, database access before the guard would throw the wrong exception.
        using var context = new DbContext(new DbContextOptions<DbContext>());
        var unitOfWork = new UnitOfWork<DbContext>(context, NullLogger<UnitOfWork<DbContext>>.Instance);

        var exception = (returnsValue, explicitIsolation) switch
        {
            (true, true) => await Assert.ThrowsAsync<ArgumentNullException>(() =>
                unitOfWork.ExecuteInTransactionAsync<int>(null!, IsolationLevel.Serializable)),
            (false, true) => await Assert.ThrowsAsync<ArgumentNullException>(() =>
                unitOfWork.ExecuteInTransactionAsync(null!, IsolationLevel.Serializable)),
            (true, false) => await Assert.ThrowsAsync<ArgumentNullException>(() =>
                unitOfWork.ExecuteInTransactionAsync<int>(null!)),
            _ => await Assert.ThrowsAsync<ArgumentNullException>(() => unitOfWork.ExecuteInTransactionAsync(null!))
        };

        Assert.Equal("action", exception.ParamName);
    }
}
