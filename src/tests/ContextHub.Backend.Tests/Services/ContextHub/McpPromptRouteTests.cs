namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Locks the Owner prompt routes independently of the MCP name-based protocol.
/// </summary>
public sealed class McpPromptRouteTests
{
    /// <summary>
    /// Uses database identifiers for detail and updates while preserving an Owner-only boundary.
    /// </summary>
    [Fact]
    public void OwnerPromptRoutesUseGuidIdentifiers()
    {
        var type = typeof(ContextHub.Api.Controllers.Admin.McpToolsController).Assembly
            .GetType("ContextHub.Api.Controllers.Admin.McpPromptsController");
        Assert.NotNull(type);
        var get = type.GetMethod("GetDetail")!;
        var put = type.GetMethod("Update")!;
        Assert.Equal("{id:guid}", get.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpGetAttribute), true)
            .Cast<Microsoft.AspNetCore.Mvc.HttpGetAttribute>().Single().Template);
        Assert.Equal("{id:guid}", put.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPutAttribute), true)
            .Cast<Microsoft.AspNetCore.Mvc.HttpPutAttribute>().Single().Template);
        Assert.Contains(type.GetCustomAttributes(true), value => value is Microsoft.AspNetCore.Authorization.AuthorizeAttribute);
    }
}
