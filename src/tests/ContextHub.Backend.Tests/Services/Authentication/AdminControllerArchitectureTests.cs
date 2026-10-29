namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Prevents administrative HTTP adapters from bypassing Application use cases.
/// </summary>
public sealed class AdminControllerArchitectureTests
{
    /// <summary>
    /// Keeps neutral controller conventions in ApiCommon; compiled dependency rules cover the Domain boundary.
    /// </summary>
    [Fact]
    public void CommonBaseTypesBelongToTheSharedProjects()
    {
        Assert.Equal("ContextHub.ApiCommon", typeof(ClientsController).BaseType!.Assembly.GetName().Name);
    }

    /// <summary>
    /// Keeps success DTO metadata on actions and error metadata in the shared/feature documentation registration.
    /// </summary>
    [Fact]
    public void ActionsDeclareOnlySuccessfulResponseTypes()
    {
        var controllers = typeof(ClientsController).Assembly.GetTypes().Where(type =>
            type.Namespace == typeof(ClientsController).Namespace && typeof(ControllerBase).IsAssignableFrom(type));
        foreach (var controller in controllers)
        {
            var statuses = controller.GetMethods().SelectMany(method => method.GetCustomAttributes<ProducesResponseTypeAttribute>());

            Assert.All(statuses, attribute => Assert.Contains(attribute.StatusCode, new[] { 200, 201 }));
        }
    }

    /// <summary>
    /// Keeps client handlers on Application service ports without persistence dependencies or audit clocks.
    /// </summary>
    [Fact]
    public void ClientHandlersUseServicesRatherThanRepositoriesAndClocks()
    {
        var handlers = typeof(CreateClientCommand).Assembly.GetTypes().Where(type =>
            type.IsClass && type.Name.EndsWith("Handler", StringComparison.Ordinal) &&
            type.Namespace?.Contains(".Clients.", StringComparison.Ordinal) == true).ToArray();
        Assert.NotEmpty(handlers);

        foreach (var handler in handlers)
        {
            var dependencies = Assert.Single(handler.GetConstructors()).GetParameters();
            Assert.DoesNotContain(
                dependencies,
                parameter => parameter.ParameterType.Name.EndsWith(
                "Repository",
                StringComparison.Ordinal));
            Assert.DoesNotContain(dependencies, parameter => parameter.ParameterType == typeof(TimeProvider));
        }
    }

    /// <summary>
    /// Keeps the native introspection adapter behind the feature service rather than querying tables itself.
    /// </summary>
    [Fact]
    public void ClientTokenAdapterUsesTheFeatureService()
    {
        var constructor = Assert.Single(typeof(ClientTokenValidationHandler)
            .GetConstructors());
        var dependencies = constructor.GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        Assert.Contains(typeof(IClientService), dependencies);
        Assert.DoesNotContain(dependencies, typeof(DbContext).IsAssignableFrom);
    }

    /// <summary>
    /// Requires each administrative controller to dispatch through the existing Mediator abstraction.
    /// </summary>
    [Fact]
    public void ControllersDependOnlyOnMediator()
    {
        var controllers = new[]
        {
            typeof(ClientsController),
            typeof(UsersController),
            typeof(RolesController),
            typeof(AuthorizationController)
        };

        // Managers and repositories belong behind Application handlers, not in the transport constructor.
        foreach (var controller in controllers)
        {
            var constructor = Assert.Single(controller.GetConstructors());
            var parameter = Assert.Single(constructor.GetParameters());
            Assert.Equal(typeof(IMediator), parameter.ParameterType);
        }
    }

    /// <summary>
    /// Requires interactive credential pages to use the same Application dispatcher as API adapters.
    /// </summary>
    [Fact]
    public void CredentialPagesDependOnlyOnMediator()
    {
        foreach (var page in new[] { typeof(LoginModel), typeof(AcceptInvitationModel) })
        {
            var constructor = Assert.Single(page.GetConstructors());
            Assert.Equal(typeof(IMediator), Assert.Single(constructor.GetParameters()).ParameterType);
        }
    }

    /// <summary>
    /// Keeps service-owned timestamps and invitation arithmetic free of a separate injected Authentication clock.
    /// </summary>
    [Fact]
    public void AuthenticationTimestampConsumersDoNotInjectAClock()
    {
        var consumers = new[]
        {
            typeof(UserService),
            typeof(UserSaveChangesInterceptor)
        };

        foreach (var consumer in consumers)
        {
            Assert.DoesNotContain(
                Assert.Single(consumer.GetConstructors()).GetParameters(),
                parameter => parameter.ParameterType == typeof(TimeProvider));
        }
    }

    /// <summary>
    /// Requires administrative use cases to return their envelope rather than making every controller wrap data again.
    /// </summary>
    [Fact]
    public void AdministrativeRequestsReturnTheCommonEnvelope()
    {
        var assembly = typeof(CreateClientCommand).Assembly;
        var requests = assembly.GetTypes().Where(type => type.IsClass &&
            typeof(ContextHub.Authentication.Application.Policies.IAdminRequest).IsAssignableFrom(type)).ToArray();
        Assert.NotEmpty(requests);

        foreach (var request in requests)
        {
            var contract = Assert.Single(
                request.GetInterfaces(),
                candidate => candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IRequest<>));
            var response = contract.GenericTypeArguments[0];

            Assert.True(response.IsGenericType, request.Name + " must return the common response envelope.");
            Assert.Equal(typeof(ApiResponse<>), response.GetGenericTypeDefinition());
        }
    }
}
