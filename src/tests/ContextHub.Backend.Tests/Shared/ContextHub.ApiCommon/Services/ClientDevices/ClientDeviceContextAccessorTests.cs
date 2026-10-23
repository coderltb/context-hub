namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.Services.ClientDevices;

/// <summary>
/// Checks optional request metadata through DI without treating it as authentication or a client allowlist.
/// </summary>
public sealed class ClientDeviceContextAccessorTests
{
    /// <summary>
    /// Leaves unavailable request metadata absent rather than inventing a device or IP address.
    /// </summary>
    [Fact]
    public void MissingContext_ReturnsAbsentMetadata()
    {
        var services = new ServiceCollection().AddLogging().AddApiInfrastructure("/api/test/v1", "1.0");
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        var metadata = scope.ServiceProvider.GetRequiredService<IClientDeviceContextAccessor>().GetCurrent();

        Assert.Null(metadata.DeviceId);
        Assert.Null(metadata.DeviceName);
        Assert.Null(metadata.DeviceType);
        Assert.Null(metadata.UserAgent);
        Assert.Null(metadata.IpAddress);
    }

    /// <summary>
    /// Keeps parallel request metadata isolated and trims optional headers without truncation or fake defaults.
    /// </summary>
    /// <returns>The asynchronous HTTP and scope-isolation assertions.</returns>
    [Fact]
    public async Task ParallelRequests_ReadOnlyTheirOwnOptionalHeaders()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddApiInfrastructure("/api/test/v1", "1.0");
        await using var app = builder.Build();
        app.MapGet("/metadata", (IClientDeviceContextAccessor accessor, HttpContext context) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
            return accessor.GetCurrent();
        });
        await app.StartAsync();
        using var client = app.GetTestClient();

        // Overlapping requests must never retain a prior device's headers in a singleton snapshot.
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/metadata");
            request.Headers.Add("X-Device-Id", $" device-{index} ");
            request.Headers.Add("X-Device-Name", new string('n', 300));
            request.Headers.Add("X-Device-Type", "   ");
            request.Headers.Add("User-Agent", "TestClient/1.0");
            using var response = await client.SendAsync(request);
            var metadata = await response.Content.ReadFromJsonAsync<ClientDeviceContextModel>();

            Assert.Equal($"device-{index}", metadata!.DeviceId);
            Assert.Equal(300, metadata.DeviceName!.Length);
            Assert.Null(metadata.DeviceType);
            Assert.Equal("TestClient/1.0", metadata.UserAgent);
            Assert.Equal("192.0.2.10", metadata.IpAddress);
        }));

        var absent = await client.GetFromJsonAsync<ClientDeviceContextModel>("/metadata");
        Assert.Null(absent!.DeviceId);
        Assert.Null(absent.UserAgent);
    }
}
