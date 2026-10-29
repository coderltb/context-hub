namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Extensions.Validations;

/// <summary>
/// Verifies materialized collection limits, duplicate detection and neutral Mediator validation failures.
/// </summary>
public sealed class CollectionValidationExtensionTests
{
    /// <summary>
    /// Treats null as optional so the owning validator decides whether the field is required.
    /// </summary>
    [Fact]
    public void OptionalRules_AcceptNull()
    {
        var validator = CreateValidator();

        var result = validator.Validate(new CollectionRequest(null));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Leaves required collection validation to the native FluentValidation rule.
    /// </summary>
    [Fact]
    public void OptionalRules_ComposeWithRequiredRule()
    {
        var validator = CreateValidator();
        validator.RuleFor(request => request.Values).NotNull();

        var result = validator.Validate(new CollectionRequest(null));

        Assert.Single(result.Errors);
    }

    /// <summary>
    /// Accepts empty collections and an inclusive maximum while rejecting the first item beyond that maximum.
    /// </summary>
    /// <param name="count">The number of fixture elements generated for this scenario.</param>
    /// <param name="expected">The expected result asserted by the scenario.</param>
    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void MaxCount_EnforcesInclusiveBoundary(int count, bool expected)
    {
        var validator = new InlineValidator<CollectionRequest>();
        validator.RuleFor(request => request.Values).MaxCount(2);
        var values = Enumerable.Range(0, count).Select(value => value.ToString()).ToArray();

        var result = validator.Validate(new CollectionRequest(values));

        Assert.Equal(expected, result.IsValid);
        if (!expected)
        {
            var error = Assert.Single(result.Errors);
            Assert.Equal("collection_too_large", error.ErrorCode);
            Assert.Contains("2", error.ErrorMessage, StringComparison.Ordinal);
            Assert.DoesNotContain("{MaxCount}", error.ErrorMessage, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Allows a zero maximum for an empty optional collection and rejects nonempty input.
    /// </summary>
    [Fact]
    public void MaxCount_ZeroAllowsOnlyEmptyCollection()
    {
        var validator = new InlineValidator<CollectionRequest>();
        validator.RuleFor(request => request.Values).MaxCount(0);

        Assert.True(validator.Validate(new CollectionRequest([])).IsValid);
        Assert.False(validator.Validate(new CollectionRequest(["one"])).IsValid);
    }

    /// <summary>
    /// Rejects invalid rule configuration before any request can reach validation.
    /// </summary>
    [Fact]
    public void MaxCount_RejectsNegativeMaximum()
    {
        var validator = new InlineValidator<CollectionRequest>();

        Assert.Throws<ArgumentOutOfRangeException>(() => validator.RuleFor(request => request.Values).MaxCount(-1));
    }

    /// <summary>
    /// Applies default equality and a caller-supplied comparer without normalizing the submitted values.
    /// </summary>
    [Fact]
    public void DistinctElements_RespectsComparer()
    {
        var defaultValidator = new InlineValidator<CollectionRequest>();
        defaultValidator.RuleFor(request => request.Values).DistinctElements();
        var foldedValidator = new InlineValidator<CollectionRequest>();
        foldedValidator.RuleFor(request => request.Values).DistinctElements(StringComparer.OrdinalIgnoreCase);
        var request = new CollectionRequest(["skill", "SKILL"]);

        Assert.True(defaultValidator.Validate(request).IsValid);
        var error = Assert.Single(foldedValidator.Validate(request).Errors);
        Assert.Equal("collection_duplicates", error.ErrorCode);
    }

    /// <summary>
    /// Rejects repeated values and repeated null items using the same equality semantics.
    /// </summary>
    [Fact]
    public void DistinctElements_RejectsRepeatedItemsIncludingNull()
    {
        var validator = new InlineValidator<NullableItemRequest>();
        validator.RuleFor(request => request.Values).DistinctElements();

        Assert.False(validator.Validate(new NullableItemRequest(["same", "same"])).IsValid);
        Assert.False(validator.Validate(new NullableItemRequest([null, null])).IsValid);
        Assert.True(validator.Validate(new NullableItemRequest([null, "one"])).IsValid);
    }

    /// <summary>
    /// Accepts the concrete materialized collection types used by ordinary request DTOs.
    /// </summary>
    /// <param name="shape">The input shape selected by the test case.</param>
    [Theory]
    [InlineData("array")]
    [InlineData("list")]
    [InlineData("read_only")]
    public void Rules_AcceptMaterializedCollectionShapes(string shape)
    {
        IReadOnlyCollection<string> values = shape switch
        {
            "array" => new[] { "one", "two" },
            "list" => new List<string> { "one", "two" },
            _ => Array.AsReadOnly(["one", "two"])
        };
        var validator = CreateValidator();

        var result = validator.Validate(new CollectionRequest(values));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Keeps fluent type inference usable when the declared DTO property is an array or List rather than an interface.
    /// </summary>
    [Fact]
    public void Rules_InferElementTypeForConcreteProperties()
    {
        var validator = new InlineValidator<ConcreteCollectionRequest>();
        validator.RuleFor(request => request.Array).MaxCount(2).DistinctElements();
        validator.RuleFor(request => request.List).MaxCount(2).DistinctElements();

        var result = validator.Validate(new ConcreteCollectionRequest(["one", "two"], ["one", "two"]));

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Allows feature validators to replace the safe shared error code and message.
    /// </summary>
    [Fact]
    public void Rules_AllowFeatureErrorOverrides()
    {
        var validator = new InlineValidator<CollectionRequest>();
        validator.RuleFor(request => request.Values)
            .MaxCount(1)
            .WithErrorCode("too_many_capabilities")
            .WithMessage("Select at most one capability.");

        var error = Assert.Single(validator.Validate(new CollectionRequest(["one", "two"])).Errors);

        Assert.Equal("too_many_capabilities", error.ErrorCode);
        Assert.Equal("Select at most one capability.", error.ErrorMessage);
    }

    /// <summary>
    /// Aggregates collection rule failures through the existing pipeline and never dispatches a rejected request.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task Rules_AggregateThroughValidationBehaviorWithoutCallingHandler()
    {
        var maximumValidator = new InlineValidator<CollectionRequest>();
        maximumValidator.RuleFor(request => request.Values).MaxCount(1);
        var distinctValidator = new InlineValidator<CollectionRequest>();
        distinctValidator.RuleFor(request => request.Values).DistinctElements();
        var behavior = new ValidationBehavior<CollectionRequest, int>(
            [maximumValidator, distinctValidator],
            NullLogger<ValidationBehavior<CollectionRequest, int>>.Instance);
        var handlerCalls = 0;

        var error = await Assert.ThrowsAsync<CoreValidationException>(() => behavior.Handle(
            new CollectionRequest(["one", "one"]),
            (_, _) =>
            {
                handlerCalls++;

                return ValueTask.FromResult(1);
            },
            CancellationToken.None).AsTask());

        Assert.Equal(0, handlerCalls);
        Assert.Equal(2, error.Errors.Count);
        Assert.All(error.Errors, failure => Assert.Equal(nameof(CollectionRequest.Values), failure.Field));
        Assert.Contains(error.Errors, failure => failure.Code == "collection_too_large" && failure.Message.Contains('1'));
        Assert.Contains(error.Errors, failure => failure.Code == "collection_duplicates");
    }

    /// <summary>
    /// Builds the real shared rules using an inclusive two-item limit and default duplicate detection.
    /// </summary>
    /// <returns>The validator for the synthetic request.</returns>
    private static InlineValidator<CollectionRequest> CreateValidator()
    {
        var validator = new InlineValidator<CollectionRequest>();
        validator.RuleFor(request => request.Values).MaxCount(2).DistinctElements();

        return validator;
    }

    /// <summary>
    /// Carries already materialized values through both rule and Mediator tests.
    /// </summary>
    /// <param name="Values">The optional materialized values.</param>
    private sealed record CollectionRequest(IReadOnlyCollection<string>? Values) : IRequest<int>;

    /// <summary>
    /// Exercises duplicate detection for nullable collection elements.
    /// </summary>
    /// <param name="Values">The materialized collection whose items may be null.</param>
    private sealed record NullableItemRequest(IReadOnlyCollection<string?> Values);

    /// <summary>
    /// Exercises generic rule inference for concrete DTO property types.
    /// </summary>
    /// <param name="Array">The materialized array.</param>
    /// <param name="List">The materialized list.</param>
    private sealed record ConcreteCollectionRequest(string[] Array, List<string> List);
}
