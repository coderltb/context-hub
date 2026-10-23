namespace ContextHub.Core.Extensions.Validations;

/// <summary>
/// Adds optional materialized collection rules while leaving required fields and item validation to the owning feature.
/// </summary>
public static class CollectionValidationExtension
{
    /// <summary>
    /// Limits an optional materialized collection to an inclusive maximum without enumerating a lazy source.
    /// </summary>
    /// <typeparam name="TModel">The feature's validated request type.</typeparam>
    /// <typeparam name="TItem">The collection's element type.</typeparam>
    /// <param name="ruleBuilder">The rule for an already materialized collection.</param>
    /// <param name="max">The nonnegative inclusive maximum count.</param>
    /// <returns>The configured rule, with error code and message still overridable by the owning validator.</returns>
    public static IRuleBuilderOptions<TModel, IReadOnlyCollection<TItem>?> MaxCount<TModel, TItem>(
        this IRuleBuilder<TModel, IReadOnlyCollection<TItem>?> ruleBuilder,
        int max)
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);
        if (max < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(max), CoreErrorConstants.ValidationErrors.MAX_COUNT_INVALID);
        }

        // Count is available without enumeration; required-field validation remains a separate feature rule.
        return ruleBuilder
            .Must((_, values, context) =>
            {
                context.MessageFormatter.AppendArgument(CoreConstants.ValidationVariables.MAX_COUNT_ARGUMENT, max);

                return values is null || values.Count <= max;
            })
            .WithErrorCode(CoreErrorConstants.ValidationErrors.COLLECTION_TOO_LARGE)
            .WithMessage(CoreErrorConstants.ValidationErrors.COLLECTION_TOO_LARGE_MESSAGE);
    }

    /// <summary>
    /// Rejects duplicate items using the selected comparer without changing values or requiring optional input.
    /// </summary>
    /// <typeparam name="TModel">The feature's validated request type.</typeparam>
    /// <typeparam name="TItem">The collection's element type.</typeparam>
    /// <param name="ruleBuilder">The rule for an already materialized collection.</param>
    /// <param name="comparer">The equality policy, or null to use the element type's default equality.</param>
    /// <returns>The configured rule, with error code and message still overridable by the owning validator.</returns>
    public static IRuleBuilderOptions<TModel, IReadOnlyCollection<TItem>?> DistinctElements<TModel, TItem>(
        this IRuleBuilder<TModel, IReadOnlyCollection<TItem>?> ruleBuilder,
        IEqualityComparer<TItem>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);

        return ruleBuilder
            .Must(values =>
            {
                if (values is null)
                {
                    return true;
                }

                // Use the caller's equality policy and stop at the first duplicate without mutating the collection.
                var seen = new HashSet<TItem>(comparer);

                return values.All(seen.Add);
            })
            .WithErrorCode(CoreErrorConstants.ValidationErrors.COLLECTION_DUPLICATES)
            .WithMessage(CoreErrorConstants.ValidationErrors.COLLECTION_DUPLICATES_MESSAGE);
    }
}
