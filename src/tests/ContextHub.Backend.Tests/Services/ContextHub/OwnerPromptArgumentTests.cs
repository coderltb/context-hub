namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Verifies pure Owner edit policy independently of persistence and transport.
/// </summary>
public sealed class OwnerPromptArgumentTests
{
    /// <summary>
    /// Omission preserves IDs; explicit removal retains a tombstone; a new draft receives a server ID.
    /// </summary>
    [Fact]
    public void OmissionAndExplicitRemovalHaveDifferentMeaning()
    {
        var first = new McpPromptArgumentDto { ArgumentId = Guid.NewGuid(), Name = "first" };
        var second = new McpPromptArgumentDto { ArgumentId = Guid.NewGuid(), Name = "second" };
        var omitted = McpPromptArgumentPolicy.Merge([first, second], [], []);
        Assert.Equal(new[] { first, second }, omitted);
        var changed = McpPromptArgumentPolicy.Merge(omitted, [first with { Description = "Owner edit" }, new() { Name = "third" }], [second.ArgumentId!.Value]);
        Assert.Equal(first.ArgumentId, changed[0].ArgumentId);
        Assert.True(changed[1].IsDeleted);
        Assert.Equal(second.ArgumentId, changed[1].ArgumentId);
        Assert.NotEqual(Guid.Empty, changed[2].ArgumentId!.Value);
    }

    /// <summary>
    /// An argument from another prompt and a conflicting removal cannot select a write target.
    /// </summary>
    [Fact]
    public void ForeignIdentityAndOverlappingEditAreRejected()
    {
        var stored = new McpPromptArgumentDto { ArgumentId = Guid.NewGuid(), Name = "saved" };
        Assert.Throws<BusinessException>(() => McpPromptArgumentPolicy.Merge([stored], [stored with { ArgumentId = Guid.NewGuid() }], []));
        Assert.Throws<BusinessException>(() => McpPromptArgumentPolicy.Merge([stored], [stored], [stored.ArgumentId!.Value]));
        Assert.Throws<BusinessException>(() => McpPromptArgumentPolicy.Merge([stored], [], [Guid.NewGuid()]));
    }

    /// <summary>
    /// A saved tombstone cannot be silently restored by another editor.
    /// </summary>
    [Fact]
    public void TombstonesCannotBeResurrectedByReplacement()
    {
        var removed = new McpPromptArgumentDto { ArgumentId = Guid.NewGuid(), Name = "removed", IsDeleted = true };
        Assert.Throws<BusinessException>(() => McpPromptArgumentPolicy.Merge([removed], [removed with { IsDeleted = false }], []));
        Assert.True(Assert.Single(McpPromptArgumentPolicy.Merge([removed], [], [])).IsDeleted);
    }
}
