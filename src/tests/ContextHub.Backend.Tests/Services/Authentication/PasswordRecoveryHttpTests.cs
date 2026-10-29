namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Exercises real Razor forms, Mediator, antiforgery and native protected cookies while replacing only recovery IO.
/// </summary>
[Collection(nameof(GlobalMappingCollection))]
public sealed class PasswordRecoveryHttpTests
{
    /// <summary>
    /// Gives the native cookie container the same HTTPS origin as the production TestServer host.
    /// </summary>
    private static readonly Uri Origin = new("https://localhost");

    /// <summary>
    /// Gives the same public continuation to unknown accounts and provider send failure.
    /// </summary>
    /// <param name="eligible">Whether the scripted account can receive recovery proof.</param>
    /// <param name="accepted">Whether the scripted mail adapter accepts a send.</param>
    /// <returns>The indistinguishable response and dispatch assertions.</returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ForgotHasOnePublicContinuation(bool eligible, bool accepted)
    {
        var recovery = new RecoveryBoundary { Eligible = eligible, Accepted = accepted };
        await using var app = await CreateHostAsync(recovery);
        using var client = app.GetTestClient();
        var cookies = new CookieContainer();
        var token = await GetFormAsync(client, AuthenticationRecoveryConstants.FORGOT_PAGE, cookies);
        using var response = await PostFormAsync(client, AuthenticationRecoveryConstants.FORGOT_PAGE, cookies, token,
            new() { ["Input.Email"] = "owner@example.test" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(AuthenticationRecoveryConstants.VERIFY_PAGE, response.Headers.Location!.OriginalString);
        Assert.Equal(1, recovery.Reservations);
        Assert.Equal(eligible ? 1 : 0, recovery.Sends);
        Assert.Null(cookies.GetCookies(Origin)[AuthenticationRecoveryConstants.RECOVERY_COOKIE]);
    }

    /// <summary>
    /// Authenticates only recovery identity, ignores posted identity overrides and never grants Admin or bypasses login.
    /// </summary>
    /// <returns>The real HTTP cookie, authorization and Application binding assertions.</returns>
    [Fact]
    public async Task VerifiedCookieBindsIdentityAndResetReturnsToLogin()
    {
        var recovery = new RecoveryBoundary();
        await using var app = await CreateHostAsync(recovery);
        using var client = app.GetTestClient();
        var cookies = new CookieContainer();
        await VerifyOtpAsync(client, cookies);
        var protectedCookie = cookies.GetCookies(Origin)[AuthenticationRecoveryConstants.RECOVERY_COOKIE]!.Value;
        var cookieOptions = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(AuthenticationRecoveryConstants.RECOVERY_SCHEME);
        var ticket = cookieOptions.TicketDataFormat.Unprotect(protectedCookie)!;
        Assert.NotNull(ticket);
        Assert.Equal(recovery.UserId.ToString(), ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Empty(ticket.Principal.FindAll(ClaimTypes.Role));
        Assert.Null(ticket.Principal.FindFirst("scope"));
        Assert.Equal(TimeSpan.FromMinutes(5), ticket.Properties.ExpiresUtc - ticket.Properties.IssuedUtc);

        using var apiRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/v1/users");
        apiRequest.Headers.Add("Cookie", CookieHeader(cookies));
        using var api = await client.SendAsync(apiRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);

        var token = await GetFormAsync(client, AuthenticationRecoveryConstants.RESET_PAGE, cookies);
        using var response = await PostFormAsync(client, AuthenticationRecoveryConstants.RESET_PAGE, cookies, token, new()
        {
            ["Input.Password"] = "New-native-Password!123",
            ["Input.ConfirmPassword"] = "New-native-Password!123",
            ["UserId"] = Guid.NewGuid().ToString(),
            ["Token"] = "forged-proof"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(AuthenticationConstants.Endpoints.LOGIN_PAGE, response.Headers.Location!.OriginalString);
        Assert.Equal(recovery.UserId, recovery.ResetRequest!.UserId);
        Assert.Equal("012345", recovery.ResetRequest.Token);
        Assert.Null(cookies.GetCookies(Origin)[AuthenticationRecoveryConstants.RECOVERY_COOKIE]);
        Assert.Null(cookies.GetCookies(Origin)["__Host-context-hub-sso"]);
    }

    /// <summary>
    /// Keeps an authenticated proof cookie for password correction without returning native policy diagnostics.
    /// </summary>
    /// <returns>The safe form and retained-cookie assertions.</returns>
    [Fact]
    public async Task NativePasswordPolicyFailureKeepsRecoveryForm()
    {
        var recovery = new RecoveryBoundary { Outcome = PasswordResetOutcome.PasswordRejected };
        await using var app = await CreateHostAsync(recovery);
        using var client = app.GetTestClient();
        var cookies = new CookieContainer();
        await VerifyOtpAsync(client, cookies);
        var token = await GetFormAsync(client, AuthenticationRecoveryConstants.RESET_PAGE, cookies);
        using var response = await PostFormAsync(client, AuthenticationRecoveryConstants.RESET_PAGE, cookies, token,
            new() { ["Input.Password"] = "weak", ["Input.ConfirmPassword"] = "weak" });
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(AuthenticationErrorConstants.PASSWORD_REJECTED_MESSAGE, html);
        Assert.NotNull(cookies.GetCookies(Origin)[AuthenticationRecoveryConstants.RECOVERY_COOKIE]);
        Assert.DoesNotContain("value=\"weak\"", html);
    }

    /// <summary>
    /// Rejects missing, tampered, expired and differently protected cookies before the reset service runs.
    /// </summary>
    /// <param name="kind">The unusable proof cookie variant.</param>
    /// <returns>The native challenge and untouched-service assertions.</returns>
    [Theory]
    [InlineData("missing")]
    [InlineData("tampered")]
    [InlineData("expired")]
    [InlineData("other-scheme")]
    public async Task ResetRequiresTheDedicatedValidCookie(string kind)
    {
        var recovery = new RecoveryBoundary();
        await using var app = await CreateHostAsync(recovery);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, AuthenticationRecoveryConstants.RESET_PAGE);
        if (kind != "missing")
        {
            var value = "untrusted-cookie";
            if (kind is "expired" or "other-scheme")
            {
                var scheme = kind == "other-scheme" ? IdentityConstants.ApplicationScheme
                    : AuthenticationRecoveryConstants.RECOVERY_SCHEME;
                var format = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(scheme).TicketDataFormat;
                var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, recovery.UserId.ToString())], scheme);
                value = format.Protect(new AuthenticationTicket(new ClaimsPrincipal(identity), new AuthenticationProperties
                {
                    IssuedUtc = DateTimeOffset.UtcNow.AddMinutes(kind == "expired" ? -10 : -1),
                    ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(kind == "expired" ? -1 : 4)
                }, scheme));
            }

            request.Headers.Add("Cookie", AuthenticationRecoveryConstants.RECOVERY_COOKIE + "=" + value);
        }

        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(AuthenticationRecoveryConstants.FORGOT_PAGE, response.Headers.Location!.OriginalString);
        Assert.Null(recovery.ResetRequest);
    }

    /// <summary>
    /// Does not issue recovery authority for an incorrect numeric proof.
    /// </summary>
    /// <returns>The rejected form and absent-cookie assertions.</returns>
    [Fact]
    public async Task InvalidOtpDoesNotIssueRecoveryAuthority()
    {
        var recovery = new RecoveryBoundary();
        await using var app = await CreateHostAsync(recovery);
        using var client = app.GetTestClient();
        var cookies = new CookieContainer();
        var token = await GetFormAsync(client, AuthenticationRecoveryConstants.VERIFY_PAGE, cookies);
        using var response = await PostFormAsync(client, AuthenticationRecoveryConstants.VERIFY_PAGE, cookies, token,
            new() { ["Input.Email"] = "owner@example.test", ["Input.OtpCode"] = "999999" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(AuthenticationErrorConstants.INVALID_PROOF_MESSAGE, await response.Content.ReadAsStringAsync());
        Assert.Null(cookies.GetCookies(Origin)[AuthenticationRecoveryConstants.RECOVERY_COOKIE]);
    }

    /// <summary>
    /// Rejects forged anonymous forms before recovery IO, independently of the Mediator validation behavior.
    /// </summary>
    /// <param name="path">The public recovery form.</param>
    /// <returns>The native antiforgery and untouched-boundary assertions.</returns>
    [Theory]
    [InlineData("/forgot-password")]
    [InlineData("/verify-password-reset-otp")]
    public async Task AnonymousRecoveryPostsRequireAntiforgery(string path)
    {
        var recovery = new RecoveryBoundary();
        await using var app = await CreateHostAsync(recovery);
        using var client = app.GetTestClient();
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = "owner@example.test", ["Input.OtpCode"] = "012345"
        });
        using var response = await client.PostAsync(path, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, recovery.Reservations);
        Assert.Equal(0, recovery.Verifications);
    }

    /// <summary>
    /// Associates server-side field errors with their inputs across every SSO form without replaying submitted passwords.
    /// </summary>
    /// <param name="path">The form rendered by the production Razor pipeline.</param>
    /// <param name="fields">The input names whose required validation must remain accessible.</param>
    /// <param name="requiresRecovery">Whether the dedicated native recovery cookie is required.</param>
    /// <returns>The real form-post and field-error association assertions.</returns>
    [Theory]
    [InlineData("/login", "UserName,Password", false)]
    [InlineData("/forgot-password", "Email", false)]
    [InlineData("/verify-password-reset-otp", "Email,OtpCode", false)]
    [InlineData("/accept-invitation", "Password,ConfirmPassword", false)]
    [InlineData("/reset-password", "Password,ConfirmPassword", true)]
    public async Task InvalidSsoFieldsDescribeTheirErrors(string path, string fields, bool requiresRecovery)
    {
        var recovery = new RecoveryBoundary();
        await using var app = await CreateHostAsync(recovery);
        using var client = app.GetTestClient();
        var cookies = new CookieContainer();
        if (requiresRecovery)
        {
            // The reset form retains its native identity boundary even when only validation is being exercised.
            await VerifyOtpAsync(client, cookies);
        }

        var token = await GetFormAsync(client, path, cookies);
        var values = fields.Split(',').ToDictionary(field => "Input." + field, _ => "");
        values["Token"] = "unusable-invitation";
        values["UserId"] = Guid.NewGuid().ToString();
        using var response = await PostFormAsync(client, path, cookies, token, values);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        foreach (var field in fields.Split(','))
        {
            var input = Regex.Match(html, "<input[^>]*name=\"Input\\." + field + "\"[^>]*>");
            Assert.True(input.Success);
            Assert.True(input.Value.Contains("aria-invalid=\"true\"", StringComparison.Ordinal), input.Value);
            Assert.Contains("aria-describedby=\"Input_" + field + "-error\"", input.Value);
            Assert.Matches("<span[^>]*id=\"Input_" + field + "-error\"[^>]*>[^<]+</span>", html);
            if (field.Contains("Password", StringComparison.Ordinal))
            {
                Assert.DoesNotContain("value=", input.Value);
            }
        }
    }

    /// <summary>
    /// Starts the production API with only the external recovery IO port replaced; native cookie protection remains real.
    /// </summary>
    /// <param name="recovery">The recording Application-owned boundary.</param>
    /// <returns>The caller-owned started host.</returns>
    private static async Task<WebApplication> CreateHostAsync(RecoveryBoundary recovery)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ClientsController).Assembly.GetName().Name,
            ContentRootPath = AuthenticationTestHostPaths.GetContentRoot(),
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(AuthenticationTestKeys.Create());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Authentication"] = "Host=localhost;Database=unused_recovery_http;Username=unused",
            ["Authentication:ClientTokenPepper"] = Convert.ToBase64String(new byte[32]),
            ["Authentication:IntrospectionClientSecret"] = new string('x', 32),
            ["Observability:Logging:MinimumLevel"] = "Fatal"
        });
        builder.Services.AddLoggingInfrastructure(builder.Configuration, "recovery-http-tests", Environments.Development);
        builder.Services.AddAuthenticationApi(builder.Configuration, builder.Environment);
        builder.Services.RemoveAll<IPasswordRecoveryService>();
        builder.Services.AddSingleton<IPasswordRecoveryService>(recovery);
        var app = builder.Build();
        app.UseAuthenticationApi();
        await app.StartAsync();
        app.GetTestServer().BaseAddress = new Uri("https://localhost");

        return app;
    }

    /// <summary>
    /// Gets a real form token and retains the corresponding native antiforgery cookie.
    /// </summary>
    /// <param name="client">The TestServer client.</param>
    /// <param name="path">The local SSO form.</param>
    /// <param name="cookies">The browser-equivalent test cookie jar.</param>
    /// <returns>The HTML-decoded native request token.</returns>
    private static async Task<string> GetFormAsync(HttpClient client, string path, CookieContainer cookies)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        var cookieHeader = CookieHeader(cookies);
        if (cookieHeader.Length > 0)
        {
            request.Headers.Add("Cookie", cookieHeader);
        }
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        UpdateCookies(response, cookies);
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "The real Razor form must emit an antiforgery request token.");

        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    /// <summary>
    /// Sends a real antiforgery-protected form and captures any native cookie transition.
    /// </summary>
    /// <param name="client">The TestServer client.</param>
    /// <param name="path">The local form endpoint.</param>
    /// <param name="cookies">The browser-equivalent cookie jar.</param>
    /// <param name="token">The request token obtained for this identity.</param>
    /// <param name="values">The submitted form fields.</param>
    /// <returns>The response owned by the caller.</returns>
    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client, string path, CookieContainer cookies, string token, Dictionary<string, string> values)
    {
        values["__RequestVerificationToken"] = token;
        values["ReturnUrl"] = "/";
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Content = new FormUrlEncodedContent(values);
        var cookieHeader = CookieHeader(cookies);
        if (cookieHeader.Length > 0)
        {
            request.Headers.Add("Cookie", cookieHeader);
        }
        var response = await client.SendAsync(request);
        UpdateCookies(response, cookies);

        return response;
    }

    /// <summary>
    /// Completes the real verification form using the boundary's accepted test proof.
    /// </summary>
    /// <param name="client">The TestServer client.</param>
    /// <param name="cookies">The cookie jar receiving the native recovery ticket.</param>
    /// <returns>The verification assertions and cookie transition.</returns>
    private static async Task VerifyOtpAsync(HttpClient client, CookieContainer cookies)
    {
        var token = await GetFormAsync(client, AuthenticationRecoveryConstants.VERIFY_PAGE, cookies);
        using var response = await PostFormAsync(client, AuthenticationRecoveryConstants.VERIFY_PAGE, cookies, token,
            new() { ["Input.Email"] = "owner@example.test", ["Input.OtpCode"] = "012345" });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var destination = new Uri(new Uri("https://localhost"), response.Headers.Location!);
        Assert.Equal(AuthenticationRecoveryConstants.RESET_PAGE, destination.AbsolutePath);
        Assert.Equal("/", QueryHelpers.ParseQuery(destination.Query)["ReturnUrl"].ToString());
    }

    /// <summary>
    /// Applies only native cookie replacement/deletion semantics needed by the test browser.
    /// </summary>
    /// <param name="response">The response containing Set-Cookie headers.</param>
    /// <param name="cookies">The caller's in-memory test jar.</param>
    private static void UpdateCookies(HttpResponseMessage response, CookieContainer cookies)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var headers))
        {
            return;
        }

        foreach (var header in headers)
        {
            cookies.SetCookies(Origin, header);
        }
    }

    /// <summary>
    /// Formats test cookies without exposing them in logs or assertion messages.
    /// </summary>
    /// <param name="cookies">The current native cookie jar.</param>
    /// <returns>The request Cookie header value.</returns>
    private static string CookieHeader(CookieContainer cookies) =>
        cookies.GetCookieHeader(Origin);

    /// <summary>
    /// Records only the recovery IO seam; this fake does not claim to verify Identity or PostgreSQL behavior.
    /// </summary>
    private sealed class RecoveryBoundary : IPasswordRecoveryService
    {
        /// <summary>
        /// Gets the fixed account selected by proof verification.
        /// </summary>
        public Guid UserId { get; } = Guid.NewGuid();

        /// <summary>
        /// Gets or sets current eligibility for the request-continuation test.
        /// </summary>
        public bool Eligible { get; init; } = true;

        /// <summary>
        /// Gets or sets provider acceptance without changing the public response.
        /// </summary>
        public bool Accepted { get; init; } = true;

        /// <summary>
        /// Gets or sets the native boundary outcome used to test safe HTTP handling.
        /// </summary>
        public PasswordResetOutcome Outcome { get; init; } = PasswordResetOutcome.Succeeded;

        /// <summary>
        /// Gets the number of send-budget reservations.
        /// </summary>
        public int Reservations { get; private set; }

        /// <summary>
        /// Gets the number of send operations after eligibility checks.
        /// </summary>
        public int Sends { get; private set; }

        /// <summary>
        /// Gets the number of proof-verification calls.
        /// </summary>
        public int Verifications { get; private set; }

        /// <summary>
        /// Gets the proof binding passed by the handler, never serialized into logs.
        /// </summary>
        public PasswordResetRequestModel? ResetRequest { get; private set; }

        /// <summary>
        /// Records the request reservation without simulating Redis authority.
        /// </summary>
        /// <param name="request">The typed recovery address.</param>
        /// <param name="cancellationToken">The caller cancellation token.</param>
        /// <returns>Permission to continue this scripted request.</returns>
        public Task<bool> ReserveRequestAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken)
        {
            Reservations++;

            return Task.FromResult(true);
        }

        /// <summary>
        /// Returns the scripted eligibility result without database access.
        /// </summary>
        /// <param name="emailAddress">The submitted address.</param>
        /// <param name="cancellationToken">The caller cancellation token.</param>
        /// <returns>The scripted approved account or null.</returns>
        public Task<PasswordRecoveryAccountModel?> GetAccountAsync(string emailAddress, CancellationToken cancellationToken) =>
            Task.FromResult(Eligible ? new PasswordRecoveryAccountModel(UserId, emailAddress) : null);

        /// <summary>
        /// Records email dispatch and returns the scripted provider acceptance.
        /// </summary>
        /// <param name="account">The approved destination.</param>
        /// <param name="cancellationToken">The caller cancellation token.</param>
        /// <returns>The scripted acceptance flag.</returns>
        public Task<bool> SendOtpAsync(PasswordRecoveryAccountModel account, CancellationToken cancellationToken)
        {
            Sends++;

            return Task.FromResult(Accepted);
        }

        /// <summary>
        /// Supplies proof metadata only for one known test code.
        /// </summary>
        /// <param name="request">The submitted proof.</param>
        /// <param name="cancellationToken">The caller cancellation token.</param>
        /// <returns>The scripted grant or null.</returns>
        public Task<PasswordRecoveryGrantModel?> VerifyAsync(VerifyPasswordResetOtpRequestDto request, CancellationToken cancellationToken)
        {
            Verifications++;

            return Task.FromResult(request.OtpCode == "012345" ? new PasswordRecoveryGrantModel(UserId, request.OtpCode) : null);
        }

        /// <summary>
        /// Records the handler's authenticated proof binding and returns the scripted outcome.
        /// </summary>
        /// <param name="request">The bound native proof and new password.</param>
        /// <param name="cancellationToken">The caller cancellation token.</param>
        /// <returns>The scripted reset outcome.</returns>
        public Task<PasswordResetOutcome> ResetAsync(PasswordResetRequestModel request, CancellationToken cancellationToken)
        {
            ResetRequest = request;

            return Task.FromResult(Outcome);
        }
    }
}
