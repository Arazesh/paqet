using System.Net;
using System.Net.Sockets;
using System.Linq;

namespace Paqet.Core;

public sealed record Address(string Host, int Port)
{
    public override string ToString()
    {
        if (string.IsNullOrEmpty(Host))
        {
            return $":{Port}";
        }

        if (Host.Contains(':') && !Host.StartsWith('['))
        {
            return $"[{Host}]:{Port}";
        }

        return $"{Host}:{Port}";
    }

    public static Address Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("Address is required.");
        }

        string host;
        string portPart;
        if (value.StartsWith('['))
        {
            var endBracket = value.IndexOf(']');
            if (endBracket < 0 || endBracket == value.Length - 1 || value[endBracket + 1] != ':')
            {
                throw new FormatException($"Invalid address: {value}");
            }

            host = value[1..endBracket];
            portPart = value[(endBracket + 2)..];
        }
        else
        {
            var firstColon = value.IndexOf(':');
            if (firstColon < 0)
            {
                throw new FormatException($"Invalid address: {value}");
            }
            if (value.IndexOf(':', firstColon + 1) >= 0)
            {
                throw new FormatException($"Invalid address: {value}");
            }

            host = value[..firstColon];
            portPart = value[(firstColon + 1)..];
        }

        if (!int.TryParse(portPart, out var port))
        {
            throw new FormatException($"Invalid address: {value}");
        }

        return new Address(host, port);
    }

    public IPAddress ResolveIPAddress(IPAddress? fallback = null)
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            return fallback ?? IPAddress.Any;
        }

        if (IPAddress.TryParse(Host, out var ip))
        {
            return ip;
        }

        var addresses = Dns.GetHostAddresses(Host);
        var ipv4 = addresses.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork);
        return ipv4 ?? addresses[0];
    }
}
