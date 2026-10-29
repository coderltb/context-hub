namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Verifies the compiled SSO surface independently of a database or product deployment.
/// </summary>
public sealed class SsoPageTests
{
    /// <summary>
    /// Requires each declared native Identity flow to have a real compiled Razor endpoint.
    /// </summary>
    /// <param name="path">The SSO page promised by the Authentication contract.</param>
    [Theory]
    [InlineData("/Pages/Identity/Account/ForgotPassword.cshtml")]
    [InlineData("/Pages/Identity/Account/VerifyPasswordResetOtp.cshtml")]
    [InlineData("/Pages/Identity/Account/ResetPassword.cshtml")]
    [InlineData("/Pages/Identity/Account/Login.cshtml")]
    [InlineData("/Pages/Identity/Account/AcceptInvitation.cshtml")]
    [InlineData("/Pages/Identity/Account/Logout.cshtml")]
    [InlineData("/Pages/Identity/Account/AccessDenied.cshtml")]
    public void ContractPagesAreCompiled(string path)
    {
        var loader = new RazorCompiledItemLoader();
        var pages = loader.LoadItems(typeof(ClientsController).Assembly);

        Assert.Contains(pages, page => page.Identifier == path);
    }
}
