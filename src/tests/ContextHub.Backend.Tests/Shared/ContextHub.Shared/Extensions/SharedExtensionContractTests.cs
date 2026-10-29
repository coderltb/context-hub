namespace ContextHub.Backend.Tests.Shared.ContextHub.Shared.Extensions;

/// <summary>
/// Guards the dependency-light shared utility surface independently of framework validation rules.
/// </summary>
public sealed class SharedExtensionContractTests
{
    /// <summary>
    /// Requires one enum utility with generic parsing and metadata methods, without numeric conversion helpers.
    /// </summary>
    [Fact]
    public void EnumExtension_ExposesOnlyApprovedGenericOperations()
    {
        var extension = typeof(SharedErrorConstants).Assembly.GetType("ContextHub.Shared.Extensions.EnumExtension");

        Assert.NotNull(extension);
        var methods = extension.GetMethods().Where(method => method.DeclaringType == extension).ToArray();
        Assert.Equal(
            ["GetDescription", "GetDisplayName", "TryParseDefinedName"],
            methods.Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(methods, method => Assert.True(method.IsGenericMethodDefinition));
    }

}
