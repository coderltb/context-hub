namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Extensions;

/// <summary>
/// Verifies optional predicate composition without confusing parameters that share a name.
/// </summary>
public sealed class ExpressionExtensionTests
{
    /// <summary>
    /// Preserves an absent filter instead of manufacturing an always-true predicate.
    /// </summary>
    [Fact]
    public void MissingConditionsRemainOptional()
    {
        Expression<Func<int, bool>>? missing = null;
        Expression<Func<int, bool>> present = value => value > 3;

        Assert.Null(missing.AndAlso(missing));
        Assert.Null(missing.OrElse(missing));
        Assert.Null(missing.Not());
        Assert.Same(present, present.AndAlso(missing));
        Assert.Same(present, missing.AndAlso(present));
        Assert.Same(present, present.OrElse(missing));
        Assert.Same(present, missing.OrElse(present));
    }

    /// <summary>
    /// Combines independently bound lambdas with short-circuit semantics and reusable negation.
    /// </summary>
    [Fact]
    public void CompositionUsesShortCircuitOperators()
    {
        Expression<Func<int, bool>> nonzero = value => value != 0;
        Expression<Func<int, bool>> divides = value => 10 / value > 1;

        var both = nonzero.AndAlso(divides)!.Compile();
        var either = nonzero.Not().OrElse(divides)!.Compile();

        Assert.False(both(0));
        Assert.True(both(2));
        Assert.False(both(10));
        Assert.True(either(0));
        Assert.False(either(10));
    }

    /// <summary>
    /// Rebinds only the outer parameter, leaving a same-named nested lambda parameter independent.
    /// </summary>
    [Fact]
    public void NestedLambdaUsesParameterIdentityRatherThanName()
    {
        Expression<Func<int, bool>> left = value => value > 0;
        var outer = Expression.Parameter(typeof(int), "value");
        var inner = Expression.Parameter(typeof(int), "value");
        var comparison = Expression.Lambda<Func<int, bool>>(Expression.GreaterThan(inner, outer), inner);
        var any = Expression.Call(typeof(Enumerable), nameof(Enumerable.Any), [typeof(int)],
            Expression.Constant(new[] { 2, 4 }), comparison);
        var right = Expression.Lambda<Func<int, bool>>(any, outer);

        var combined = left.AndAlso(right)!.Compile();

        Assert.False(combined(0));
        Assert.True(combined(3));
        Assert.False(combined(5));
    }

    /// <summary>
    /// Prevents a parameter from one operand capturing an independently bound nested parameter in the other.
    /// </summary>
    /// <param name="useOr">Whether to exercise disjunction with a false left operand.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompositionDoesNotCaptureAnExistingNestedParameter(bool useOr)
    {
        var shared = Expression.Parameter(typeof(int), "value");
        var outer = Expression.Parameter(typeof(int), "outer");
        var left = Expression.Lambda<Func<int, bool>>(
            useOr ? Expression.LessThan(shared, Expression.Constant(0))
                : Expression.GreaterThan(shared, Expression.Constant(0)), shared);

        // The right operand intentionally binds the exact parameter object used by the left operand.
        // Choosing either existing outer parameter as the merged parameter would capture this nested binding.
        var nested = Expression.Lambda<Func<int, bool>>(Expression.GreaterThan(shared, outer), shared);
        var any = Expression.Call(typeof(Enumerable), nameof(Enumerable.Any), [typeof(int)],
            Expression.Constant(new[] { 2, 4 }), nested);
        var right = Expression.Lambda<Func<int, bool>>(any, outer);

        var combined = useOr ? left.OrElse(right) : left.AndAlso(right);

        Assert.True(combined!.Compile()(3));
        Assert.False(combined.Compile()(5));
    }
}
