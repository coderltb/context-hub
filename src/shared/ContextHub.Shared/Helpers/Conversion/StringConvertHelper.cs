namespace ContextHub.Shared.Helpers.Conversion;

/// <summary>
/// Converts scalar text without invoking JSON serialization or changing public vocabularies.
/// </summary>
public static class StringConvertHelper
{
    /// <summary>
    /// Converts supported scalar types; absent input is valid only for nullable and string targets.
    /// </summary>
    /// <typeparam name="T">The requested scalar type.</typeparam>
    /// <param name="source">The optional input text.</param>
    /// <param name="value">The converted value, or default on failure.</param>
    /// <param name="provider">The culture policy; defaults to invariant culture.</param>
    /// <returns>Whether conversion succeeded without lossy enum coercion.</returns>
    public static bool TryConvert<T>(string? source, out T? value, IFormatProvider? provider = null)
    {
        value = default;
        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (source is null || (target != typeof(string) && string.IsNullOrWhiteSpace(source)))
        {
            return Nullable.GetUnderlyingType(typeof(T)) is not null || typeof(T) == typeof(string);
        }

        // Restrict conversion to scalars. Unknown reference types must use the DI-owned JSON service explicitly.
        provider ??= CultureInfo.InvariantCulture;
        object? converted;
        if (target.IsEnum)
        {
            if (!EnumExtension.TryParseDefinedName(target, source, out converted))
            {
                return false;
            }
        }
        else if (target == typeof(string))
        {
            converted = source;
        }
        else if (target == typeof(Guid))
        {
            if (!Guid.TryParse(source, out var parsed))
            {
                return false;
            }

            converted = parsed;
        }
        else if (target == typeof(DateTime))
        {
            if (!DateTime.TryParse(source, provider, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                return false;
            }

            converted = parsed;
        }
        else if (target == typeof(DateTimeOffset))
        {
            if (!DateTimeOffset.TryParse(source, provider, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                return false;
            }

            converted = parsed;
        }
        else if (target == typeof(DateOnly))
        {
            if (!DateOnly.TryParse(source, provider, DateTimeStyles.None, out var parsed))
            {
                return false;
            }

            converted = parsed;
        }
        else if (target == typeof(TimeOnly))
        {
            if (!TimeOnly.TryParse(source, provider, DateTimeStyles.None, out var parsed))
            {
                return false;
            }

            converted = parsed;
        }
        else if (target == typeof(TimeSpan))
        {
            if (!TimeSpan.TryParse(source, provider, out var parsed))
            {
                return false;
            }

            converted = parsed;
        }
        else if (target.IsPrimitive || target == typeof(decimal))
        {
            try
            {
                converted = Convert.ChangeType(source, target, provider);
            }
            catch (Exception exception) when (exception is FormatException or OverflowException or InvalidCastException)
            {
                // Malformed scalar input is an expected TryConvert failure; provider/configuration errors still propagate.
                return false;
            }
        }
        else
        {
            return false;
        }

        value = (T?)converted;

        return true;
    }
}

