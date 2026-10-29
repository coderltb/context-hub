namespace ContextHub.Authentication.Application.Dtos.Users;

/// <summary>
/// Creates an Owner with explicit credentials; roles and security state remain server-owned.
/// </summary>
/// <param name="userName">The submitted login name.</param>
/// <param name="email">The unique recovery email address.</param>
/// <param name="password">The initial password, consumed only by native Identity and never logged.</param>
public sealed class CreateUserRequestDto(
    string userName,
    string email,
    string password)
{
    /// <summary>
    /// Gets the submitted login name.
    /// </summary>
    /// <example>example.owner</example>
    public string UserName { get; } = userName;

    /// <summary>
    /// Gets the unique recovery email address.
    /// </summary>
    /// <example>owner@example.test</example>
    public string Email { get; } = email;

    /// <summary>
    /// Gets the initial password, consumed only by native Identity and never logged.
    /// </summary>
    /// <example>ExampleOnly!2026</example>
    public string Password { get; } = password;
}
