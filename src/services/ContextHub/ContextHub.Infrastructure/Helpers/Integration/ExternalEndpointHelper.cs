namespace ContextHub.Infrastructure.Helpers.Integration;

/// <summary>
/// Connects only to the public addresses actually resolved for an HTTPS request.
/// </summary>
public static class ExternalEndpointHelper
{
    /// <summary>
    /// Rejects private, local, documentation and multicast address ranges.
    /// </summary>
    /// <param name="address">The address resolved for the actual socket connection.</param>
    /// <returns>Whether the address may be used by a hosted integration.</returns>
    public static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }
        if (IPAddress.IsLoopback(address))
        {
            return false;
        }
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] != 0 && bytes[0] != 10 && bytes[0] != 127 && bytes[0] < 224 &&
                !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127) &&
                !(bytes[0] == 169 && bytes[1] == 254) &&
                !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) &&
                !(bytes[0] == 192 && bytes[1] == 168) &&
                !(bytes[0] == 192 && bytes[1] == 0 && bytes[2] is 0 or 2) &&
                !(bytes[0] == 198 && bytes[1] is 18 or 19) &&
                !(bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) &&
                !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
        }
        return address.AddressFamily == AddressFamily.InterNetworkV6 && bytes[0] is >= 0x20 and <= 0x3f &&
            !address.Equals(IPAddress.IPv6Any) && !address.Equals(IPAddress.IPv6None) &&
            !address.IsIPv6LinkLocal && !address.IsIPv6SiteLocal && !address.IsIPv6Multicast &&
            (bytes[0] & 0xfe) != 0xfc &&
            !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8) &&
            !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0 && bytes[3] == 0) &&
            !(bytes[0] == 0x20 && bytes[1] == 0x02);
    }

    /// <summary>
    /// Builds the native handler whose DNS checks bind directly to the opened socket.
    /// </summary>
    /// <returns>An owned native handler without redirects or proxy bypasses.</returns>
    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(1),
        MaxConnectionsPerServer = 16,
        ConnectCallback = ConnectAsync
    };

    /// <summary>
    /// Resolves and opens a public address while native TLS retains hostname validation.
    /// </summary>
    /// <param name="context">The actual native connection endpoint.</param>
    /// <param name="cancellationToken">Cancels DNS and socket work.</param>
    /// <returns>The owned connected network stream.</returns>
    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
        {
            throw new HttpRequestException("The hosted endpoint is not permitted.");
        }

        // The same checked address array is passed to ConnectAsync; no second DNS lookup can rebind it.
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            // Ownership transfers only after connection succeeds.
            socket.Dispose();
            throw;
        }
    }
}
