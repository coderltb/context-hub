namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Validates externally supplied offsets before repository queries while retaining the largest native Skip boundary.
/// </summary>
public sealed class PaginationValidationTests
{
    /// <summary>
    /// Keeps the last representable offset valid and rejects the next page for both list endpoints.
    /// </summary>
    /// <param name="pageSize">The requested bounded page size.</param>
    /// <returns>The validator boundary assertions without a database or service call.</returns>
    [Theory]
    [InlineData(20)]
    [InlineData(100)]
    public async Task UsersAndClientsRejectOffsetsOutsideNativeSkip(int pageSize)
    {
        var lastPage = int.MaxValue / pageSize + 1;
        var clients = new GetClientsQueryValidator();
        var users = new GetUsersQueryValidator();

        Assert.True((await clients.ValidateAsync(new GetClientsQuery(new() { Page = lastPage, PageSize = pageSize }))).IsValid);
        Assert.True((await users.ValidateAsync(new GetUsersQuery(new() { Page = lastPage, PageSize = pageSize }))).IsValid);
        Assert.False((await clients.ValidateAsync(new GetClientsQuery(new() { Page = lastPage + 1, PageSize = pageSize }))).IsValid);
        Assert.False((await users.ValidateAsync(new GetUsersQuery(new() { Page = lastPage + 1, PageSize = pageSize }))).IsValid);
    }
}
