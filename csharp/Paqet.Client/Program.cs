using System.Net;
using Paqet.Core;
using Paqet.Socks;
using Paqet.Transport.Kcp;

namespace Paqet.Client;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        if (args.Length < 1)
        {
            PrintUsage();
            return;
        }

        var mode = args[0].ToLowerInvariant();
        var transport = new KcpTransport();

        if (mode == "socks" && args.Length >= 3)
        {
            var listen = Address.Parse(args[1]);
            var server = Address.Parse(args[2]);
            var socks = new Socks5Server(new IPEndPoint(listen.ResolveIPAddress(), listen.Port), transport, server);
            Console.WriteLine($"SOCKS5 listening on {listen} -> server {server}");
            await socks.StartAsync();
            return;
        }

        if (mode == "forward" && args.Length >= 5)
        {
            var protocol = args[1].ToLowerInvariant();
            var listen = Address.Parse(args[2]);
            var target = Address.Parse(args[3]);
            var server = Address.Parse(args[4]);
            if (protocol == "tcp")
            {
                Console.WriteLine($"TCP forward {listen} -> {target} via {server}");
                await Forwarders.RunTcpForwardAsync(listen, target, server, transport);
                return;
            }

            if (protocol == "udp")
            {
                Console.WriteLine($"UDP forward {listen} -> {target} via {server}");
                await Forwarders.RunUdpForwardAsync(listen, target, server, transport);
                return;
            }
        }

        PrintUsage();
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  Paqet.Client socks <listenHost:port> <serverHost:port>");
        Console.WriteLine("  Paqet.Client forward <tcp|udp> <listenHost:port> <targetHost:port> <serverHost:port>");
    }
}
