namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Checks use-case ownership and safe translation at the native MCP boundary.
/// </summary>
public sealed class ApplicationBoundaryTests
{
    /// <summary>
    /// Checks compiled dependencies across the actual Context Hub Application assembly.
    /// </summary>
    [Fact]
    public void ContextHubApplicationDoesNotDependOnPersistenceOrInfrastructure()
    {
        var types = Types.InAssembly(typeof(ManagedToolRankingPolicy).Assembly);
        Assert.NotEmpty(types.GetTypes());
        var result = types.ShouldNot().HaveDependencyOnAny(
            "ContextHub.Infrastructure", "ContextHub.Domain.Entities", "ContextHub.Core.Interfaces.Persistence",
            "Microsoft.EntityFrameworkCore").GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>
    /// Keeps authenticated decisions and input rules out of the quality-signal IO adapter.
    /// </summary>
    [Fact]
    public void QualitySignalInfrastructureDoesNotDependOnAuthorizationOrValidation()
    {
        var dependencies = Assert.Single(typeof(QualitySignalService).GetConstructors()).GetParameters();

        Assert.DoesNotContain(dependencies, parameter => parameter.ParameterType == typeof(IAuthService));
        Assert.DoesNotContain(dependencies, parameter =>
            parameter.ParameterType.IsGenericType && parameter.ParameterType.GetGenericTypeDefinition() == typeof(IValidator<>));
    }

    /// <summary>
    /// Returns only the declared MCP validation error when a use-case validator rejects otherwise valid JSON.
    /// </summary>
    /// <returns>The safe structured result and unchanged input assertions.</returns>
    [Fact]
    public async Task ApplicationValidationDoesNotExposeFieldDetailsAtTheMcpBoundary()
    {
        var mediator = DispatchProxy.Create<IMediator, ValidationMediator>();
        var adapter = new McpAdapter(mediator, new HttpContextAccessor(), NullLogger<McpAdapter>.Instance);
        var arguments = JsonSerializer.SerializeToElement(new { query = "public reference" });

        var result = await adapter.CallToolAsync("catalog_search", arguments, CancellationToken.None);

        Assert.True(result.IsError);
        var output = result.StructuredContent!.Value;
        Assert.Equal("validation_error", output.GetProperty("error").GetProperty("code").GetString());
        Assert.DoesNotContain("sensitive-field-detail", output.GetRawText(), StringComparison.Ordinal);
        Assert.Equal("public reference", arguments.GetProperty("query").GetString());
    }

    /// <summary>
    /// Substitutes the completed contract read and the neutral validation failure only.
    /// </summary>
    public class ValidationMediator : DispatchProxy
    {
        /// <summary>
        /// Keeps the production contract evaluator while rejecting business dispatch with a Core failure.
        /// </summary>
        /// <param name="targetMethod">The native Mediator method.</param>
        /// <param name="args">The protected request and cancellation token.</param>
        /// <returns>The paired contract snapshot for discovery.</returns>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (args![0] is GetMcpToolContractsQuery request)
            {
                IReadOnlyList<McpToolContractModel> contracts = McpContractFixture.ReadContracts()
                    .Where(contract => contract.Name == request.ToolName).ToArray();
                return ValueTask.FromResult(contracts);
            }

            throw new CoreValidationException([new ValidationError("query", "invalid", "sensitive-field-detail")]);
        }
    }
}
