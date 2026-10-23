namespace ContextHub.Core.Extensions;

/// <summary>
/// Composes optional typed filters while preserving expression trees for database translation.
/// </summary>
public static class ExpressionExtension
{
    /// <summary>
    /// Searches selected nullable string fields with a literal keyword, combining their matches with OR.
    /// </summary>
    /// <typeparam name="TEntity">The entity whose searchable fields are selected by the caller.</typeparam>
    /// <param name="searchTerm">The unchanged keyword; null or whitespace means no text condition.</param>
    /// <param name="selectors">Required valid string selectors only when the keyword is active.</param>
    /// <returns>A provider-readable text predicate, or null when the keyword is absent.</returns>
    public static Expression<Func<TEntity, bool>>? SearchText<TEntity>(
        string? searchTerm, params Expression<Func<TEntity, string?>>[]? selectors)
    {
        return BuildTextSearch(searchTerm, selectors, null);
    }

    /// <summary>
    /// Shares literal text matching between scalar fields and mapped child collections without expanding caller scope.
    /// </summary>
    /// <typeparam name="TEntity">The entity whose mapped text sources were explicitly selected.</typeparam>
    /// <param name="searchTerm">The unchanged keyword; blank skips all text selectors.</param>
    /// <param name="selectors">Optional root or related-object string selectors.</param>
    /// <param name="collectionSelectors">Optional mapped collections of nullable strings, searched with Any.</param>
    /// <returns>The scalar/collection OR group, or null when no keyword is active.</returns>
    internal static Expression<Func<TEntity, bool>>? BuildTextSearch<TEntity>(string? searchTerm,
        Expression<Func<TEntity, string?>>[]? selectors,
        Expression<Func<TEntity, IEnumerable<string?>>>[]? collectionSelectors)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return null;
        }

        // An active search must never silently become an unrestricted read because its fields were omitted.
        if ((selectors is not { Length: > 0 } && collectionSelectors is not { Length: > 0 }) ||
            selectors?.Any(selector => selector is null) == true ||
            collectionSelectors?.Any(selector => selector is null) == true)
        {
            throw new ArgumentException(CoreErrorConstants.QueryErrors.TEXT_FIELDS_REQUIRED, nameof(selectors));
        }

        // Capturing the keyword lets EF parameterize the value instead of embedding a new constant in each tree.
        // Contains remains provider-native; no case conversion, wildcard language or client evaluation is added.
        Expression<Func<string?, bool>> contains = value => value != null && value.Contains(searchTerm);
        Expression<Func<TEntity, bool>>? predicate = null;
        foreach (var selector in selectors ?? [])
        {
            var body = new ParameterReplacementVisitor(contains.Parameters[0], selector.Body).Visit(contains.Body);
            var condition = Expression.Lambda<Func<TEntity, bool>>(body, selector.Parameters);
            predicate = predicate.OrElse(condition);
        }

        // Keep each collection as a provider-readable Any subquery: empty collections and null strings cannot match.
        // Reuse the captured keyword and string condition; do not materialize children or rewrite caller field filters.
        foreach (var selector in collectionSelectors ?? [])
        {
            var body = Expression.Call(typeof(Enumerable), nameof(Enumerable.Any), [typeof(string)],
                selector.Body, contains);
            var condition = Expression.Lambda<Func<TEntity, bool>>(body, selector.Parameters);
            predicate = predicate.OrElse(condition);
        }

        return predicate;
    }

    /// <summary>
    /// Requires every supplied field condition while treating null expressions as absent conditions.
    /// </summary>
    /// <typeparam name="TEntity">The entity filtered by the caller's typed conditions.</typeparam>
    /// <param name="filters">Optional conditions; false, zero and explicit null comparisons remain real filters.</param>
    /// <returns>The conjunction of available filters, or null when none was supplied.</returns>
    public static Expression<Func<TEntity, bool>>? SearchFields<TEntity>(
        params Expression<Func<TEntity, bool>>?[]? filters)
    {
        Expression<Func<TEntity, bool>>? predicate = null;
        if (filters is null)
        {
            return null;
        }

        // Only the missing expression is skipped; interpreting its captured values belongs to the caller.
        foreach (var filter in filters)
        {
            predicate = predicate.AndAlso(filter);
        }

        return predicate;
    }

    /// <summary>
    /// Requires both available conditions; an absent condition leaves the other unchanged.
    /// </summary>
    /// <typeparam name="T">The filtered value type.</typeparam>
    /// <param name="left">The current condition, or null when filtering has not started.</param>
    /// <param name="right">The additional condition, or null when it is not applicable.</param>
    /// <returns>A short-circuit conjunction, the sole condition, or null when both are absent.</returns>
    public static Expression<Func<T, bool>>? AndAlso<T>(
        this Expression<Func<T, bool>>? left, Expression<Func<T, bool>>? right)
    {
        return Combine(left, right, ExpressionType.AndAlso);
    }

    /// <summary>
    /// Accepts either available condition without turning an absent condition into an always-true filter.
    /// </summary>
    /// <typeparam name="T">The filtered value type.</typeparam>
    /// <param name="left">The current condition, or null when filtering has not started.</param>
    /// <param name="right">The alternative condition, or null when it is not applicable.</param>
    /// <returns>A short-circuit disjunction, the sole condition, or null when both are absent.</returns>
    public static Expression<Func<T, bool>>? OrElse<T>(
        this Expression<Func<T, bool>>? left, Expression<Func<T, bool>>? right)
    {
        return Combine(left, right, ExpressionType.OrElse);
    }

    /// <summary>
    /// Negates an existing condition without manufacturing a filter when none was supplied.
    /// </summary>
    /// <typeparam name="T">The filtered value type.</typeparam>
    /// <param name="predicate">The condition to negate, or null.</param>
    /// <returns>The negated expression, or null for an absent condition.</returns>
    public static Expression<Func<T, bool>>? Not<T>(this Expression<Func<T, bool>>? predicate)
    {
        return predicate is null
            ? null
            : Expression.Lambda<Func<T, bool>>(Expression.Not(predicate.Body), predicate.Parameters);
    }

    /// <summary>
    /// Unifies the outer parameter before creating a provider-readable binary expression.
    /// </summary>
    /// <typeparam name="T">The filtered value type.</typeparam>
    /// <param name="left">The existing optional condition.</param>
    /// <param name="right">The additional optional condition.</param>
    /// <param name="operation">The short-circuit boolean operator selected by the public method.</param>
    /// <returns>The combined expression, preserving null and single-condition identities.</returns>
    private static Expression<Func<T, bool>>? Combine<T>(
        Expression<Func<T, bool>>? left, Expression<Func<T, bool>>? right, ExpressionType operation)
    {
        if (left is null || right is null)
        {
            return left ?? right;
        }

        // A fresh binding cannot collide with a nested parameter already present in either operand.
        // Rebind both roots without invoking or compiling lambdas so EF still sees the complete tree.
        var parameter = Expression.Parameter(typeof(T), "entity");
        var leftBody = new ParameterReplacementVisitor(left.Parameters[0], parameter).Visit(left.Body);
        var rightBody = new ParameterReplacementVisitor(right.Parameters[0], parameter).Visit(right.Body);

        return Expression.Lambda<Func<T, bool>>(
            Expression.MakeBinary(operation, leftBody, rightBody), parameter);
    }

    /// <summary>
    /// Replaces only one parameter identity, leaving unrelated nested parameters intact.
    /// </summary>
    /// <param name="source">The outer parameter used by the incoming condition.</param>
    /// <param name="target">The new outer parameter or selected field expression used in its place.</param>
    private sealed class ParameterReplacementVisitor(ParameterExpression source, Expression target)
        : ExpressionVisitor
    {
        /// <summary>
        /// Matches parameter objects rather than their names, which nested lambdas may reuse.
        /// </summary>
        /// <param name="node">The visited parameter.</param>
        /// <returns>The replacement only for the selected identity; otherwise the original parameter.</returns>
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return ReferenceEquals(node, source) ? target : node;
        }
    }
}
