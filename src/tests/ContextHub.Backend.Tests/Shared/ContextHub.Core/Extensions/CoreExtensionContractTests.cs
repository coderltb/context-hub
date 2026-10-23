namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Extensions;

/// <summary>
/// Guards the public utility surface and prevents collection rules from accepting deferred enumerable sources.
/// </summary>
public sealed class CoreExtensionContractTests
{
    /// <summary>
    /// Requires one enum utility with generic parsing and metadata methods, without numeric conversion helpers.
    /// </summary>
    [Fact]
    public void EnumExtension_ExposesOnlyApprovedGenericOperations()
    {
        var extension = typeof(CoreErrorConstants).Assembly.GetType("ContextHub.Core.Extensions.EnumExtension");

        Assert.NotNull(extension);
        var methods = extension.GetMethods().Where(method => method.DeclaringType == extension).ToArray();
        Assert.Equal(
            new[] { "GetDescription", "GetDisplayName", "TryParseDefinedName" },
            methods.Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.All(methods, method => Assert.True(method.IsGenericMethodDefinition));
    }

    /// <summary>
    /// Requires collection rules to accept materialized read-only collections instead of arbitrary lazy enumerables.
    /// </summary>
    [Fact]
    public void CollectionRules_RequireMaterializedCollections()
    {
        var extension = typeof(CoreErrorConstants).Assembly.GetType(
            "ContextHub.Core.Extensions.Validations.CollectionValidationExtension");

        Assert.NotNull(extension);
        var methods = extension.GetMethods().Where(method => method.DeclaringType == extension).ToArray();
        Assert.Equal(
            new[] { "DistinctElements", "MaxCount" },
            methods.Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal));

        foreach (var method in methods)
        {
            var propertyType = method.GetParameters()[0].ParameterType.GenericTypeArguments[1];
            Assert.Equal(typeof(IReadOnlyCollection<>), propertyType.GetGenericTypeDefinition());
        }
    }
}
