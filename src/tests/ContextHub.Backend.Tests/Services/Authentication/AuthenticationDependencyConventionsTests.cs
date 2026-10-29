namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Keeps feature service constructors focused and their dependency roles explicit without requiring a live provider.
/// </summary>
public sealed class AuthenticationDependencyConventionsTests
{
    /// <summary>
    /// Enforces the reviewed seven-dependency limit on authored Infrastructure services.
    /// </summary>
    [Fact]
    public void ServiceConstructorsStayWithinSevenDependencies()
    {
        var services = typeof(AuthenticationDbContext).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && !type.IsNested && type.Namespace is { } name &&
                name.StartsWith("ContextHub.Authentication.Infrastructure.Services", StringComparison.Ordinal));
        var oversized = services.SelectMany(type => type.GetConstructors())
            .Where(constructor => constructor.GetParameters().Length > 7)
            .Select(constructor => constructor.DeclaringType!.Name + ": " + constructor.GetParameters().Length);

        Assert.Empty(oversized);
    }

    /// <summary>
    /// Allows native Identity managers while preventing feature services from depending on another concrete feature service.
    /// </summary>
    [Fact]
    public void FeatureServicesAreConsumedThroughInterfaces()
    {
        var assemblies = new[] { typeof(AuthenticationDbContext).Assembly, typeof(ClientsController).Assembly };
        var concreteDependencies = assemblies.SelectMany(assembly => assembly.GetTypes())
            .Where(type => !type.IsAbstract && !type.IsNested)
            .SelectMany(type => type.GetConstructors())
            .SelectMany(constructor => constructor.GetParameters().Select(parameter => (constructor, parameter)))
            .Where(item => item.parameter.ParameterType.IsClass &&
                item.parameter.ParameterType.Namespace is { } name &&
                name.StartsWith("ContextHub.Authentication.Infrastructure.Services", StringComparison.Ordinal))
            .Select(item => item.constructor.DeclaringType!.Name + " -> " + item.parameter.ParameterType.Name);

        Assert.Empty(concreteDependencies);
    }

    /// <summary>
    /// Checks the user-selected role names for native managers instead of ambiguous plural dependency identifiers.
    /// </summary>
    [Fact]
    public void NativeManagersHaveUnambiguousDependencyNames()
    {
        var expected = new Dictionary<string, string>
        {
            ["UserManager`1"] = "userManager",
            ["RoleManager`1"] = "roleManager",
            ["SignInManager`1"] = "signInManager",
            ["IOpenIddictAuthorizationManager"] = "authorizationManager",
            ["IOpenIddictTokenManager"] = "tokenManager",
            ["IOpenIddictApplicationManager"] = "applicationManager",
            ["IOpenIddictScopeManager"] = "scopeManager"
        };
        var mismatches = typeof(AuthenticationDbContext).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && !type.IsNested)
            .SelectMany(type => type.GetConstructors())
            .SelectMany(constructor => constructor.GetParameters().Select(parameter => (constructor, parameter)))
            .Where(item => expected.TryGetValue(item.parameter.ParameterType.Name, out var name) && item.parameter.Name != name)
            .Select(item => item.constructor.DeclaringType!.Name + "." + item.parameter.Name);

        Assert.Empty(mismatches);
    }

    /// <summary>
    /// Keeps reusable mail transport and persisted account types in the layers that own their contracts.
    /// </summary>
    [Fact]
    public void SharedEmailAndUserEntityHaveCorrectOwners()
    {
        Assert.Equal("ContextHub.Core", typeof(IEmailService).Assembly.GetName().Name);
        Assert.Equal("ContextHub.Shared", typeof(EmailRequestDto).Assembly.GetName().Name);
        Assert.Equal("ContextHub.Authentication.Domain", typeof(User).Assembly.GetName().Name);
        Assert.Equal("User", typeof(User).Name);
    }

    /// <summary>
    /// Ensures specific repositories emit their own logger category rather than the generic base category.
    /// </summary>
    [Fact]
    public void RepositoryLoggersUseTheirEnclosingType()
    {
        var repositories = typeof(AuthenticationDbContext).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && type.Namespace is { } name &&
                name.StartsWith("ContextHub.Authentication.Infrastructure.Repositories", StringComparison.Ordinal));

        foreach (var type in repositories)
        {
            var loggers = type.GetConstructors().SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => parameter.ParameterType.IsGenericType &&
                    parameter.ParameterType.GetGenericTypeDefinition() == typeof(ILogger<>));

            Assert.All(loggers, parameter => Assert.Equal(type, parameter.ParameterType.GenericTypeArguments[0]));
        }
    }

}
