namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Checks save-time state conversion against the relational model without claiming database integration.
/// </summary>
public sealed class UserSaveChangesTests
{
    /// <summary>
    /// Converts removal into a narrow update while discarding unrelated edits and preserving the concurrency predicate.
    /// </summary>
    /// <param name="asynchronous">Whether the asynchronous save boundary is selected.</param>
    /// <param name="detectChanges">Whether EF performs automatic change detection.</param>
    /// <returns>The state, column-selection and dependent-preservation assertions.</returns>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task UserDeletionBecomesNarrowDeactivation(bool asynchronous, bool detectChanges)
    {
        using var context = CreateContext();
        var created = DateTimeOffset.UtcNow.AddDays(-2);
        var user = new User
        {
            Id = Guid.NewGuid(), UserName = "owner", CreatedAt = created, UpdatedAt = created,
            PasswordHash = "original-hash", SecurityStamp = "original-security", ConcurrencyStamp = "original-version"
        };
        context.Attach(user);
        var membership = new IdentityUserRole<Guid> { UserId = user.Id, RoleId = Guid.NewGuid() };
        var claim = new IdentityUserClaim<Guid> { Id = 1, UserId = user.Id, ClaimType = "test", ClaimValue = "value" };
        var login = new IdentityUserLogin<Guid> { UserId = user.Id, LoginProvider = "test", ProviderKey = "key" };
        var token = new IdentityUserToken<Guid> { UserId = user.Id, LoginProvider = "test", Name = "authenticator", Value = "key" };
        context.AttachRange(membership, claim, login, token);
        user.PasswordHash = "discarded-hash";
        user.SecurityStamp = "discarded-security";
        user.CreatedAt = created.AddYears(-1);
        context.ChangeTracker.AutoDetectChangesEnabled = detectChanges;

        context.Remove(user);
        if (asynchronous)
        {
            await context.SaveChangesAsync();
        }
        else
        {
            context.SaveChanges();
        }

        Assert.False(user.IsActive);
        Assert.Equal(created, user.CreatedAt);
        Assert.Equal("original-hash", user.PasswordHash);
        Assert.Equal("original-security", user.SecurityStamp);
        Assert.NotEqual("original-version", user.ConcurrencyStamp);
        Assert.Equal("original-version", context.Entry(user).Property(value => value.ConcurrencyStamp).OriginalValue);
        Assert.Equal(new[] { "ConcurrencyStamp", "IsActive", "UpdatedAt" },
            context.Entry(user).Properties.Where(property => property.IsModified).Select(property => property.Metadata.Name).Order().ToArray());
        Assert.All(new object[] { membership, claim, login, token }, entity => Assert.Equal(EntityState.Unchanged, context.Entry(entity).State));
    }

    /// <summary>
    /// Leaves explicit dependent deletion intact when its parent is retained as an inactive account.
    /// </summary>
    /// <returns>The independent child intent assertions.</returns>
    [Fact]
    public async Task ExplicitChildDeletionSurvivesParentDeactivation()
    {
        using var context = CreateContext();
        var user = new User { Id = Guid.NewGuid() };
        var token = new IdentityUserToken<Guid> { UserId = user.Id, LoginProvider = "test", Name = "obsolete" };
        context.AttachRange(user, token);

        context.Remove(token);
        context.RemoveRange(user);
        await context.SaveChangesAsync();

        Assert.False(user.IsActive);
        Assert.Equal(EntityState.Deleted, context.Entry(token).State);
    }

    /// <summary>
    /// Converts every User in one range without leaving a physical-delete entry behind.
    /// </summary>
    /// <returns>The complete range state assertions.</returns>
    [Fact]
    public async Task RemoveRangeDeactivatesAllUsers()
    {
        using var context = CreateContext();
        var users = new[] { new User { Id = Guid.NewGuid() }, new User { Id = Guid.NewGuid() } };
        context.Users.AttachRange(users);

        context.Users.RemoveRange(users);
        await context.SaveChangesAsync();

        Assert.All(users, user =>
        {
            Assert.False(user.IsActive);
            Assert.Equal(EntityState.Modified, context.Entry(user).State);
        });
    }

    /// <summary>
    /// Uses the production provider/model and suppresses SQL only after production save interceptors ran.
    /// </summary>
    /// <returns>A test-owned context that never connects to a database.</returns>
    private static AuthenticationDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<AuthenticationDbContext>();
        builder.UseNpgsql("Host=127.0.0.1;Database=unused_state_test;Username=unused")
            .UseOpenIddict<Guid>()
            .AddInterceptors(new UserSaveChangesInterceptor(), new SuppressSaveInterceptor());

        return new AuthenticationDbContext(builder.Options);
    }

    /// <summary>
    /// Prevents database I/O while retaining the actual save-time state transition for unit assertions.
    /// </summary>
    private sealed class SuppressSaveInterceptor : SaveChangesInterceptor
    {
        /// <summary>
        /// Suppresses only command execution; preceding save interceptors still run.
        /// </summary>
        /// <param name="eventData">The saving context.</param>
        /// <param name="result">The preceding interceptor result.</param>
        /// <returns>A suppressed save result.</returns>
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
            InterceptionResult<int>.SuppressWithResult(0);

        /// <summary>
        /// Provides the same no-I/O boundary for asynchronous saves.
        /// </summary>
        /// <param name="eventData">The saving context.</param>
        /// <param name="result">The preceding interceptor result.</param>
        /// <param name="cancellationToken">The save cancellation token.</param>
        /// <returns>A suppressed asynchronous save result.</returns>
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
    }
}
