namespace ContextHub.Backend.Tests.Common.Hosting;

/// <summary>
/// Runs a built API entry point with its own configuration and console streams, without a database fixture.
/// </summary>
internal sealed class ApiHostProcess : IAsyncDisposable
{
    /// <summary>
    /// Owns only the child process started by this fixture.
    /// </summary>
    private readonly Process _process;

    /// <summary>
    /// Drains stdout while the host runs so redirected pipes cannot block startup.
    /// </summary>
    private readonly Task<string> _output;

    /// <summary>
    /// Drains the optional text sink independently of stdout.
    /// </summary>
    private readonly Task<string> _error;

    /// <summary>
    /// Retains the HTTP client and asynchronous pipe readers for one child host.
    /// </summary>
    /// <param name="process">The child API process owned by this test fixture.</param>
    /// <param name="address">The local HTTP address exposed by the spawned API host.</param>
    private ApiHostProcess(Process process, Uri address)
    {
        _process = process;
        _output = process.StandardOutput.ReadToEndAsync();
        _error = process.StandardError.ReadToEndAsync();
        Client = new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(2) };
    }

    /// <summary>
    /// Gets the client used to exercise actual Kestrel routes.
    /// </summary>
    public HttpClient Client { get; }

    /// <summary>
    /// Starts the existing entry point using the build configuration of the test assembly.
    /// </summary>
    /// <param name="service">The service directory below src/services.</param>
    /// <param name="application">The API project and assembly name.</param>
    /// <param name="environment">The appsettings environment to exercise.</param>
    /// <param name="format">An optional deployment format override.</param>
    /// <returns>A fixture that must be disposed even when readiness checks fail.</returns>
    public static ApiHostProcess Start(string service, string application, string environment, string? format = null)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ContextHub.sln")))
        {
            root = root.Parent;
        }

        Assert.NotNull(root);
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var project = Path.Combine(root!.FullName, "src", "services", service, application);
        var assembly = Path.Combine(project, "bin", configuration, "net10.0", $"{application}.dll");
        Assert.True(File.Exists(assembly), "Build the API projects before running host smoke tests.");

        // Reserve an ephemeral loopback port briefly; readiness checks also catch a rare competing allocation.
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var address = new Uri($"http://127.0.0.1:{port}");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = project,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(assembly);
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add(address.ToString());
        start.Environment["ASPNETCORE_ENVIRONMENT"] = environment;
        start.Environment["DOTNET_ENVIRONMENT"] = environment;
        if (format is not null)
        {
            start.Environment["Observability__Logging__ConsoleFormat"] = format;
        }

        return new ApiHostProcess(Process.Start(start)!, address);
    }

    /// <summary>
    /// Waits for the preserved scaffold route rather than relying on a particular startup log message.
    /// </summary>
    /// <returns>A task that completes when the spawned host responds, or faults if startup fails.</returns>
    public async Task WaitUntilReadyAsync()
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(30))
        {
            Assert.False(_process.HasExited, "The API host exited before accepting a request.");
            try
            {
                using var response = await Client.GetAsync("/weatherforecast");
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                // Kestrel may not have bound the port yet; retry only this safe test readiness request.
                await Task.Delay(100);
                continue;
            }

            await Task.Delay(100);
        }

        Assert.Fail("The API host did not become ready within the smoke-test deadline.");
    }

    /// <summary>
    /// Stops the owned host before joining its pipe readers and returning both console streams.
    /// </summary>
    /// <returns>The captured stdout and stderr text after the host exits.</returns>
    public async Task<(string Output, string Error)> ReadLogsAsync()
    {
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
        }

        await _process.WaitForExitAsync();

        return (await _output, await _error);
    }

    /// <summary>
    /// Releases only this fixture's process and HTTP resources, including on assertion failures.
    /// </summary>
    /// <returns>A task that completes after fixture-owned resources have been released.</returns>
    public async ValueTask DisposeAsync()
    {
        await ReadLogsAsync();
        Client.Dispose();
        _process.Dispose();
    }
}
