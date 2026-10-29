namespace ContextHub.Backend.Tests.Conventions;

/// <summary>
/// Verifies the agreed shared ownership and technical-first namespace layout without exercising infrastructure providers.
/// </summary>
public sealed class SharedLayoutConventionsTests
{
    /// <summary>
    /// Keeps pure utilities, neutral enums and common data models in the dependency-light Shared assembly.
    /// </summary>
    /// <param name="type">The reusable type whose public ownership must stay stable.</param>
    [Theory]
    [InlineData(typeof(TokenHelper))]
    [InlineData(typeof(CodeGenerationHelper))]
    [InlineData(typeof(ExpirationHelper))]
    [InlineData(typeof(StringConvertHelper))]
    [InlineData(typeof(StringExtension))]
    [InlineData(typeof(EnumExtension))]
    [InlineData(typeof(ObjectExtension))]
    [InlineData(typeof(ClaimExtension))]
    [InlineData(typeof(ExpressionExtension))]
    [InlineData(typeof(BusinessErrorKind))]
    [InlineData(typeof(ClientDeviceContextModel))]
    [InlineData(typeof(ValidationError))]
    [InlineData(typeof(SearchOptions<object>))]
    [InlineData(typeof(DapperCommandOptions))]
    public void PureReusableTypesBelongToShared(Type type)
    {
        Assert.Equal("ContextHub.Shared", type.Assembly.GetName().Name);
    }

    /// <summary>
    /// Prevents capability folders from recreating parallel technical hierarchies inside Core and ApiCommon.
    /// </summary>
    [Fact]
    public void FrameworkComponentsUseOneTechnicalHierarchy()
    {
        var types = typeof(CoreServiceRegistration).Assembly.GetTypes()
            .Concat(typeof(ApiResponseBoundary).Assembly.GetTypes());
        var obsolete = types.Where(type => type.Namespace is { } name &&
            (name.StartsWith("ContextHub.Core.Persistence", StringComparison.Ordinal) ||
             name.StartsWith("ContextHub.Core.Messaging", StringComparison.Ordinal) ||
             name.StartsWith("ContextHub.Core.Validation", StringComparison.Ordinal) ||
             name.StartsWith("ContextHub.ApiCommon.OpenApi", StringComparison.Ordinal)))
            .Select(type => type.FullName);

        Assert.Empty(obsolete);
        Assert.Equal("ContextHub.Core.Interfaces.Persistence", typeof(IGenericRepository<>).Namespace);
        Assert.Equal("ContextHub.Core.Interfaces.Persistence", typeof(IUnitOfWork).Namespace);
        Assert.Equal("ContextHub.Core.Interfaces.Persistence", typeof(IDapperService).Namespace);
    }

    /// <summary>
    /// Makes utility names describe their technical role without renaming semantic value objects.
    /// </summary>
    [Fact]
    public void UtilityClassesUseResponsibilitySuffixes()
    {
        var assemblies = new[]
        {
            typeof(CoreServiceRegistration).Assembly,
            typeof(ApiResponseBoundary).Assembly,
            typeof(ObservabilityServiceRegistration).Assembly,
            typeof(ApiError).Assembly,
            typeof(CacheKey).Assembly,
            typeof(ClientsController).Assembly,
            typeof(CreateClientCommand).Assembly,
            typeof(AuthenticationDbContext).Assembly
        };
        var types = assemblies.SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsClass && !type.IsNested && type.Namespace is not null)
            .ToArray();

        Assert.NotEmpty(types);

        foreach (var type in types)
        {
            var segments = type.Namespace!.Split('.');

            if (segments.Contains("Extensions"))
            {
                Assert.True(type.Name.EndsWith("Extension", StringComparison.Ordinal) ||
                    type.Name.EndsWith("Extensions", StringComparison.Ordinal), type.FullName);
            }

            if (segments.Contains("Helpers"))
            {
                Assert.EndsWith("Helper", type.Name, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// Keeps service registration builders beside DI composition rather than immutable configuration metadata.
    /// </summary>
    [Fact]
    public void ServiceRegistrationBuildersBelongToDependencyInjection()
    {
        var registrationTypes = typeof(ApiResponseBoundary).Assembly.GetTypes()
            .Where(type => type.IsPublic && type.GetConstructors()
                .Any(constructor => constructor.GetParameters()
                    .Any(parameter => parameter.ParameterType == typeof(IServiceCollection))))
            .Where(type => type.Name.EndsWith("Registration", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(registrationTypes);
        Assert.All(registrationTypes, type =>
            Assert.Contains(".DependencyInjection", type.Namespace, StringComparison.Ordinal));
    }
}
