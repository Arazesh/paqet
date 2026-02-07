using Paqet.Socks;

namespace Paqet.Client;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("Paqet.Client scaffold. TODO: load config and start SOCKS/forwarders.");
        var socks = new Socks5Server();
        await socks.StartAsync();
    }
}
