namespace ContextHub.Authentication.Application.Constants;

/// <summary>
/// Defines the public account ordering vocabulary independently of database column names.
/// </summary>
public static class AuthenticationQueryConstants
{
    /// <summary>
    /// Identifies username ordering.
    /// </summary>
    public const string USER_NAME = "userName";

    /// <summary>
    /// Identifies contact-address ordering.
    /// </summary>
    public const string EMAIL = "email";

    /// <summary>
    /// Identifies active-state ordering.
    /// </summary>
    public const string IS_ACTIVE = "isActive";

    /// <summary>
    /// Identifies creation-time ordering.
    /// </summary>
    public const string CREATED_AT = "createdAt";

    /// <summary>
    /// Identifies update-time ordering.
    /// </summary>
    public const string UPDATED_AT = "updatedAt";

    /// <summary>
    /// Identifies ascending ordering.
    /// </summary>
    public const string ASCENDING = SharedConstants.SortVariables.ASCENDING;

    /// <summary>
    /// Identifies descending ordering.
    /// </summary>
    public const string DESCENDING = SharedConstants.SortVariables.DESCENDING;

    /// <summary>
    /// Preserves the default account ordering in the public request DTO.
    /// </summary>
    public const string DEFAULT_USER_SORT = USER_NAME + " " + ASCENDING;

    /// <summary>
    /// Lists the only public fields the account query may translate into typed selectors.
    /// </summary>
    public static readonly IReadOnlyList<string> USER_FIELDS = Array.AsReadOnly([USER_NAME, EMAIL, IS_ACTIVE, CREATED_AT, UPDATED_AT]);
}
