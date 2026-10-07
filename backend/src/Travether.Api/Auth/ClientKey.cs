using System.Net;
using System.Net.Sockets;

namespace Travether.Api.Auth;

/// <summary>Rate-limit partition for an anonymous client: its IPv4 address, or its IPv6 /64 (one home or phone gets a whole /64).</summary>
public static class ClientKey
{
    public static string For(HttpContext http) => Of(http.Connection.RemoteIpAddress);

    public static string Of(IPAddress? ip)
    {
        if (ip is null)
        {
            return "unknown";
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        if (ip.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return ip.ToString();
        }

        var bytes = ip.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }
}
