namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Locks approved Owner identities independently of public MCP identifiers.
/// </summary>
public sealed class OwnerConsoleContractTests
{
    /// <summary>
    /// Requires stable database identity alongside the public catalog key.
    /// </summary>
    [Fact]
    public void CatalogAdminUsesUuidAndPublicKey()
    {
        Assert.Equal(typeof(Guid), typeof(PackageResponseDto).GetProperty("PackageId")!.PropertyType);
        Assert.NotNull(typeof(PackageResponseDto).GetProperty("Key"));
        Assert.Null(typeof(VersionResponseDto).GetProperty("VersionId"));
        Assert.Equal(typeof(string), typeof(VersionResponseDto).GetProperty("Version")!.PropertyType);
        Assert.NotNull(typeof(LifecycleVersionRequestDto).GetProperty("ReplacementVersion"));
    }

    /// <summary>
    /// Requires stable argument identity and explicit removal rather than omission.
    /// </summary>
    [Fact]
    public void PromptArgumentsHaveIdentityAndRemoval()
    {
        Assert.NotNull(typeof(McpPromptArgumentDto).GetProperty("ArgumentId"));
        Assert.NotNull(typeof(McpPromptArgumentDto).GetProperty("IsDeleted"));
        Assert.NotNull(typeof(UpdateMcpPromptRequestDto).GetProperty("RemovedArgumentIds"));
    }

    /// <summary>
    /// Requires persisted deletion independently of temporary disablement.
    /// </summary>
    [Fact]
    public void ExternalConnectionHasLifecycle()
    {
        Assert.NotNull(typeof(IntegrationExternalMcp).GetProperty("Status"));
        Assert.NotNull(typeof(ExternalMcpResponseDto).GetProperty("ConnectionId"));
    }

    /// <summary>
    /// Requires role identity and database-backed client filters.
    /// </summary>
    [Fact]
    public void AuthenticationUsesRoleIdsAndClientFilters()
    {
        Assert.NotNull(typeof(UserRolesRequestDto).GetProperty("RoleIds"));
        Assert.NotNull(typeof(UserResponseDto).GetProperty("RoleIds"));
        Assert.NotNull(typeof(GetClientsRequestDto).GetProperty("Query"));
        Assert.NotNull(typeof(GetClientsRequestDto).GetProperty("IsActive"));
    }

    /// <summary>
    /// Requires the read-only Owner detail and public preview used by the approved screens.
    /// </summary>
    [Fact]
    public void OwnerDetailsExposeReviewedData()
    {
        var detail = typeof(McpToolResponseDto).Assembly.GetType("ContextHub.Application.Dtos.Mcp.McpToolDetailResponseDto");
        Assert.NotNull(detail);
        Assert.NotNull(detail.GetProperty("InputSchema"));
        Assert.NotNull(detail.GetProperty("OutputSchema"));
        Assert.NotNull(detail.GetProperty("Resources"));
        Assert.NotNull(typeof(VersionResponseDto).GetProperty("PublicPreview"));
        Assert.NotNull(typeof(DashboardPackageResponseDto).GetProperty("PackageType"));
    }
}
