using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Conay.Utils;

internal static class DnsHelper
{
    internal static async Task<string> ResolveToIpv4Async(string address, ILogger logger)
    {
        if (string.IsNullOrEmpty(address))
            return address;

        int colonIndex = address.LastIndexOf(':');
        string host = colonIndex >= 0 ? address[..colonIndex] : address;
        string port = colonIndex >= 0 ? address[colonIndex..] : string.Empty;

        if (IPAddress.TryParse(host, out IPAddress? parsed) && parsed.AddressFamily == AddressFamily.InterNetwork)
            return address;

        try
        {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork);
            if (addresses.Length > 0)
                return addresses[0] + port;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve hostname '{Host}' to IPv4!", host);
        }

        return address;
    }
}