namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Verifies User deactivation SQL, dependent preservation and native concurrency against disposable PostgreSQL.
/// </summary>
/// <param name="postgreSql">The serialized collection's disposable database; never a product connection.</param>
[Collection(PostgreSqlCollection.NAME)]
public sealed class UserSaveChangesPostgreSqlTests(PostgreSqlFixture postgreSql) : IAsyncLifetime
{
    /// <summary>
    /// Recreates only the container-owned schema using the production Authentication relational model.
    /// </summary>
    /// <returns>The isolated schema initialization.</returns>
    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Leaves database container ownership with the serialized collection fixture.
    /// </summary>
    /// <returns>An already completed cleanup task.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Enriches only the current user page and avoids membership I/O when that page is empty.
    /// </summary>
    /// <param name="pageNumber">The requested page, including one beyond the final item.</param>
    /// <param name="expectedItems">The expected page cardinality.</param>
    /// <returns>The role grouping, total and relational command assertions.</returns>
    [Theory]
    [InlineData(1, 2)]
    [InlineData(3, 0)]
    public async Task UserPageGroupsRolesAndSkipsMembershipReadWhenEmpty(int pageNumber, int expectedItems)
    {
        new ServiceCollection().AddMapping();
        await using (var seed = CreateContext())
        {
            var first = new User { Id = Guid.NewGuid(), UserName = "alpha" };
            var second = new User { Id = Guid.NewGuid(), UserName = "beta" };
            var third = new User { Id = Guid.NewGuid(), UserName = "gamma" };
            var owner = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Owner", NormalizedName = "OWNER" };
            var reader = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Reader", NormalizedName = "READER" };
            seed.AddRange(first, second, third, owner, reader,
                new IdentityUserRole<Guid> { UserId = first.Id, RoleId = owner.Id },
                new IdentityUserRole<Guid> { UserId = first.Id, RoleId = reader.Id },
                new IdentityUserRole<Guid> { UserId = third.Id, RoleId = reader.Id });
            await seed.SaveChangesAsync();
        }

        var observer = new UserCommandObserver();
        await using var context = CreateContext(observer);
        var repository = new UserRepository(context, NullLogger<UserRepository>.Instance);
        var page = await repository.GetPageAsync(
            new() { Page = pageNumber, PageSize = 2, Sort = "userName asc" }, CancellationToken.None);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(expectedItems, page.Items.Count);
        Assert.Equal(expectedItems == 0 ? 2 : 3, observer.Commands.Count);
        Assert.Equal(expectedItems == 0 ? 0 : 1,
            observer.Commands.Count(command => command.Contains("identity.\"UserRole\"", StringComparison.Ordinal)));
        Assert.Empty(context.ChangeTracker.Entries());
        if (expectedItems > 0)
        {
            Assert.Equal(["alpha", "beta"], page.Items.Select(user => user.UserName));
            Assert.Equal(["Owner", "Reader"], page.Items[0].Roles.Order());
            Assert.Empty(page.Items[1].Roles);
        }
    }

    /// <summary>
    /// Persists only deactivation columns and keeps loaded Identity dependents through both native save paths.
    /// </summary>
    /// <param name="asynchronous">Whether to use the asynchronous save operation.</param>
    /// <param name="detectChanges">Whether automatic change detection remains enabled.</param>
    /// <returns>The actual SQL, persisted scalar and dependent-preservation assertions.</returns>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task RemovalWritesOnlyDeactivationColumnsAndRetainsDependents(bool asynchronous, bool detectChanges)
    {
        var id = await SeedUserAsync();
        var observer = new UserCommandObserver();
        await using var context = CreateContext(observer);
        var user = await context.Users.SingleAsync(value => value.Id == id);
        var createdAt = user.CreatedAt;
        var version = user.ConcurrencyStamp;
        await context.UserRoles.Where(value => value.UserId == id).LoadAsync();
        await context.UserClaims.Where(value => value.UserId == id).LoadAsync();
        await context.UserLogins.Where(value => value.UserId == id).LoadAsync();
        await context.UserTokens.Where(value => value.UserId == id).LoadAsync();

        // Pending credential or creation edits must be discarded when the operation's intent is User removal.
        user.PasswordHash = "discarded-hash";
        user.SecurityStamp = "discarded-stamp";
        user.UserName = "discarded-name";
        user.CreatedAt = createdAt.AddYears(-1);
        context.ChangeTracker.AutoDetectChangesEnabled = detectChanges;
        observer.Commands.Clear();
        context.Remove(user);

        if (asynchronous)
        {
            await context.SaveChangesAsync();
        }
        else
        {
            context.SaveChanges();
        }

        var sql = Assert.Single(observer.Commands, command =>
            command.Contains("UPDATE ", StringComparison.Ordinal) && command.Contains("\"User\"", StringComparison.Ordinal));
        Assert.Contains("\"IsActive\" =", sql);
        Assert.Contains("\"UpdatedAt\" =", sql);
        Assert.Contains("\"ConcurrencyStamp\" =", sql);
        Assert.DoesNotContain("\"PasswordHash\"", sql);
        Assert.DoesNotContain("\"SecurityStamp\"", sql);
        Assert.DoesNotContain("\"CreatedAt\"", sql);
        Assert.DoesNotContain("\"UserName\"", sql);
        Assert.DoesNotContain(observer.Commands, command => command.Contains("DELETE", StringComparison.Ordinal));

        // Read with an independent context so tracker state cannot masquerade as durable database behavior.
        await using var verification = CreateContext();
        var persisted = await verification.Users.AsNoTracking().SingleAsync(value => value.Id == id);
        Assert.False(persisted.IsActive);
        Assert.Equal("original-hash", persisted.PasswordHash);
        Assert.Equal("original-stamp", persisted.SecurityStamp);
        Assert.Equal("owner", persisted.UserName);
        Assert.Equal(createdAt, persisted.CreatedAt);
        Assert.NotEqual(version, persisted.ConcurrencyStamp);
        Assert.Equal(TimeSpan.Zero, persisted.UpdatedAt.Offset);
        Assert.Equal(1, await verification.UserRoles.CountAsync(value => value.UserId == id));
        Assert.Equal(1, await verification.UserClaims.CountAsync(value => value.UserId == id));
        Assert.Equal(1, await verification.UserLogins.CountAsync(value => value.UserId == id));
        Assert.Equal(1, await verification.UserTokens.CountAsync(value => value.UserId == id));
    }

    /// <summary>
    /// Rejects an obsolete removal attempt using the original native concurrency stamp instead of overwriting a newer version.
    /// </summary>
    /// <returns>The native concurrency failure and committed-version preservation assertions.</returns>
    [Fact]
    public async Task StaleRemovalRetainsNativeConcurrencyFailure()
    {
        var id = await SeedUserAsync();
        await using var stale = CreateContext();
        await using var winner = CreateContext();
        var staleUser = await stale.Users.SingleAsync(value => value.Id == id);
        var winnerUser = await winner.Users.SingleAsync(value => value.Id == id);
        winner.Remove(winnerUser);
        await winner.SaveChangesAsync();
        var committedVersion = winnerUser.ConcurrencyStamp;

        // Both instances started with the same version; the second save must still use that original predicate.
        stale.Remove(staleUser);
        var failure = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());

        Assert.Same(staleUser, Assert.Single(failure.Entries).Entity);
        await using var verification = CreateContext();
        var persisted = await verification.Users.AsNoTracking().SingleAsync(value => value.Id == id);
        Assert.False(persisted.IsActive);
        Assert.Equal(committedVersion, persisted.ConcurrencyStamp);
        Assert.Equal(1, await verification.UserTokens.CountAsync(value => value.UserId == id));
    }

    /// <summary>
    /// Preserves an explicit dependent deletion while converting removal of its User into deactivation.
    /// </summary>
    /// <returns>The durable dependent-deletion and retained-account assertions.</returns>
    [Fact]
    public async Task ExplicitDependentRemovalIsNotReversed()
    {
        var id = await SeedUserAsync();
        await using var context = CreateContext();
        var user = await context.Users.SingleAsync(value => value.Id == id);
        var token = await context.UserTokens.SingleAsync(value => value.UserId == id);

        context.Remove(token);
        context.RemoveRange(user);
        await context.SaveChangesAsync();

        await using var verification = CreateContext();
        Assert.False((await verification.Users.SingleAsync(value => value.Id == id)).IsActive);
        Assert.Empty(await verification.UserTokens.Where(value => value.UserId == id).ToListAsync());
        Assert.Equal(1, await verification.UserRoles.CountAsync(value => value.UserId == id));
        Assert.Equal(1, await verification.UserClaims.CountAsync(value => value.UserId == id));
        Assert.Equal(1, await verification.UserLogins.CountAsync(value => value.UserId == id));
    }

    /// <summary>
    /// Keeps physical role removal and its native cascading relationships outside User-specific deactivation.
    /// </summary>
    /// <returns>The native role cascade and unaffected User assertions.</returns>
    [Fact]
    public async Task NonUserDeletionRetainsNativeCascade()
    {
        var id = await SeedUserAsync();
        await using var context = CreateContext();
        var role = await context.Roles.SingleAsync();
        context.RoleClaims.Add(new IdentityRoleClaim<Guid>
        {
            RoleId = role.Id,
            ClaimType = "fixture-permission",
            ClaimValue = "fixture-value"
        });
        await context.SaveChangesAsync();
        await context.UserRoles.LoadAsync();
        await context.RoleClaims.LoadAsync();

        // Deferred timing changes when native cascading runs, not the physical-delete semantics of other entity types.
        context.Remove(role);
        await context.SaveChangesAsync();

        await using var verification = CreateContext();
        Assert.Empty(await verification.Roles.ToListAsync());
        Assert.Empty(await verification.RoleClaims.ToListAsync());
        Assert.Empty(await verification.UserRoles.ToListAsync());
        Assert.True((await verification.Users.SingleAsync(value => value.Id == id)).IsActive);
        Assert.Equal(1, await verification.UserTokens.CountAsync(value => value.UserId == id));
    }

    /// <summary>
    /// Seeds one account and each native Identity dependent needed to expose accidental cascading deletion.
    /// </summary>
    /// <returns>The identifier persisted in the disposable database.</returns>
    private async Task<Guid> SeedUserAsync()
    {
        await using var context = CreateContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "owner",
            NormalizedUserName = "OWNER",
            PasswordHash = "original-hash",
            SecurityStamp = "original-stamp"
        };
        var role = new IdentityRole<Guid> { Id = Guid.NewGuid(), Name = "Owner", NormalizedName = "OWNER" };
        context.AddRange(
            user,
            role,
            new IdentityUserRole<Guid> { UserId = user.Id, RoleId = role.Id },
            new IdentityUserClaim<Guid> { UserId = user.Id, ClaimType = "fixture-claim", ClaimValue = "fixture-value" },
            new IdentityUserLogin<Guid> { UserId = user.Id, LoginProvider = "fixture-provider", ProviderKey = "fixture-key" },
            new IdentityUserToken<Guid>
            {
                UserId = user.Id,
                LoginProvider = "fixture-provider",
                Name = "AuthenticatorKey",
                Value = "fixture-authenticator-material"
            });
        await context.SaveChangesAsync();

        return user.Id;
    }

    /// <summary>
    /// Retains the production model, provider and interceptor; command observation records SQL text without parameter values.
    /// </summary>
    /// <param name="observer">The optional test-owned command observer.</param>
    /// <returns>The caller-owned context for the disposable PostgreSQL fixture.</returns>
    private AuthenticationDbContext CreateContext(UserCommandObserver? observer = null)
    {
        var options = new DbContextOptionsBuilder<AuthenticationDbContext>();
        options.UseNpgsql(postgreSql.ConnectionString)
            .UseOpenIddict<Guid>()
            .AddInterceptors(new UserSaveChangesInterceptor());

        if (observer is not null)
        {
            options.AddInterceptors(observer);
        }

        return new AuthenticationDbContext(options.Options);
    }

    /// <summary>
    /// Observes real relational reader commands without replacing execution or retaining bound credential values.
    /// </summary>
    private sealed class UserCommandObserver : DbCommandInterceptor
    {
        /// <summary>
        /// Gets the command text executed by this test-owned context.
        /// </summary>
        public List<string> Commands { get; } = [];

        /// <summary>
        /// Observes synchronous commands while returning the native interception result unchanged.
        /// </summary>
        /// <param name="command">The relational command.</param>
        /// <param name="eventData">The native command metadata.</param>
        /// <param name="result">The current interception result.</param>
        /// <returns>The unchanged interception result.</returns>
        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Commands.Add(command.CommandText);

            return result;
        }

        /// <summary>
        /// Observes asynchronous commands without changing cancellation or provider execution.
        /// </summary>
        /// <param name="command">The relational command.</param>
        /// <param name="eventData">The native command metadata.</param>
        /// <param name="result">The current interception result.</param>
        /// <param name="cancellationToken">The native command cancellation token.</param>
        /// <returns>The unchanged asynchronous interception result.</returns>
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);

            return ValueTask.FromResult(result);
        }
    }
}
