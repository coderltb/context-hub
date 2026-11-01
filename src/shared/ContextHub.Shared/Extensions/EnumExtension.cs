namespace ContextHub.Shared.Extensions;

/// <summary>
/// Provides declared enum-name parsing and presentation metadata without defining public wire vocabularies.
/// </summary>
public static class EnumExtension
{
    /// <summary>
    /// Parses one declared name using ordinal case-insensitive comparison, rejecting numeric and combined input.
    /// </summary>
    /// <typeparam name="TEnum">The service-owned enum type.</typeparam>
    /// <param name="name">The optional member name, with surrounding whitespace allowed.</param>
    /// <param name="value">The declared value on success; otherwise the enum's default value.</param>
    /// <returns>Whether the input names exactly one declared member.</returns>
    public static bool TryParseDefinedName<TEnum>(
        string? name,
        out TEnum value)
        where TEnum : struct, Enum
    {
        var success = TryParseDefinedName(typeof(TEnum), name, out var parsed);
        value = success ? (TEnum)parsed! : default;

        return success;
    }

    /// <summary>
    /// Applies the same declared-name rule when a nullable scalar converter resolves the enum type at runtime.
    /// </summary>
    /// <param name="enumType">The actual enum type, never a nullable wrapper.</param>
    /// <param name="name">The optional single member name.</param>
    /// <param name="value">The declared enum value on success.</param>
    /// <returns>Whether one declared member was matched.</returns>
    internal static bool TryParseDefinedName(
        Type enumType,
        string? name,
        out object? value)
    {
        value = null;
        if (!enumType.IsEnum || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        // Native TryParse also accepts numbers and flags expressions; match a declared name before parsing.
        var candidate = name.Trim();
        var declaredName = Enum.GetNames(enumType).FirstOrDefault(declared =>
            string.Equals(candidate, declared, StringComparison.OrdinalIgnoreCase));
        if (declaredName is null)
        {
            return false;
        }

        value = Enum.Parse(enumType, declaredName);

        return true;
    }

    /// <summary>
    /// Gets description metadata, falling back to the enum's native representation without validating its value.
    /// </summary>
    /// <typeparam name="TEnum">The enum type whose presentation metadata is read.</typeparam>
    /// <param name="value">The value to describe, including an undefined value.</param>
    /// <returns>The description, declared name or native undefined-value representation.</returns>
    public static string GetDescription<TEnum>(this TEnum value)
        where TEnum : struct, Enum
    {
        var name = Enum.GetName(value);
        if (name is null)
        {
            // Undefined values and unnamed flags combinations have no single member attribute to inspect.
            return value.ToString();
        }

        var description = typeof(TEnum).GetField(name)?.GetCustomAttribute<DescriptionAttribute>();

        return description?.Description ?? name;
    }

    /// <summary>
    /// Gets display metadata, falling back to the enum's native representation without changing contract identifiers.
    /// </summary>
    /// <typeparam name="TEnum">The enum type whose presentation metadata is read.</typeparam>
    /// <param name="value">The value to display, including an undefined value.</param>
    /// <returns>The display name, declared name or native undefined-value representation.</returns>
    public static string GetDisplayName<TEnum>(this TEnum value)
        where TEnum : struct, Enum
    {
        var name = Enum.GetName(value);
        if (name is null)
        {
            // Display fallback is not a validation result and must not imply authorization or compatibility.
            return value.ToString();
        }

        var display = typeof(TEnum).GetField(name)?.GetCustomAttribute<DisplayAttribute>();

        return display?.GetName() ?? name;
    }
}
