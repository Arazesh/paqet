using System.Net;
using System.Net.Sockets;
using Paqet.Core;
using Paqet.Transport.Kcp;

namespace Paqet.Server;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: Paqet.Server <listenHost:port>");
            return;
        }

        var listen = Address.Parse(args[0]);
        var transport = new KcpTransport();
        await using var listener = await transport.ListenAsync(listen);
        Console.WriteLine($"Server listening on {listen}");
        while (true)
        {
            var connection = await listener.AcceptAsync();
            _ = HandleConnectionAsync(connection);
        }
    }

    private static async Task HandleConnectionAsync(IConnection connection)
    {
        await using var _ = connection;
        var stream = await connection.AcceptStreamAsync();
        await using var __ = stream;
        var header = await ProtocolHeader.ReadAsync(stream);
        var flags = (IReadOnlyList<TcpFlags>?)null;
        if (header.Type == Core.ProtocolType.TcpFlags)
        {
            flags = header.Flags;
            header = await ProtocolHeader.ReadAsync(stream);
        }
        if (header.Type == Core.ProtocolType.Tcp && header.Address is not null)
        {
            await HandleTcpAsync(stream, header.Address);
        }
        else if (header.Type == Core.ProtocolType.Udp && header.Address is not null)
        {
            await HandleUdpAsync(stream, header.Address);
        }
    }

    private static async Task HandleTcpAsync(IStream tunnel, Address target)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(target.Host, target.Port).ConfigureAwait(false);
        var network = client.GetStream();
        var clientStream = new NetworkStreamAdapter(network);
        var copyToTarget = StreamCopy.CopyAsync(tunnel, clientStream);
        var copyToClient = StreamCopy.CopyAsync(clientStream, tunnel);
        await Task.WhenAny(copyToTarget, copyToClient).ConfigureAwait(false);
    }

    private static async Task HandleUdpAsync(IStream tunnel, Address target)
    {
        using var udp = new UdpClient(0);
        udp.Connect(target.Host, target.Port);
        var receiveTask = RelayUdpToTunnelAsync(udp, tunnel);
        var sendTask = RelayTunnelToUdpAsync(udp, tunnel);
        await Task.WhenAny(receiveTask, sendTask).ConfigureAwait(false);
    }

    private static async Task RelayTunnelToUdpAsync(UdpClient udp, IStream tunnel)
    {
        var buffer = new byte[1500];
        while (true)
        {
            var read = await tunnel.ReadAsync(buffer).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await udp.SendAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
        }
    }

    private static async Task RelayUdpToTunnelAsync(UdpClient udp, IStream tunnel)
    {
        while (true)
        {
            var result = await udp.ReceiveAsync().ConfigureAwait(false);
            await tunnel.WriteAsync(result.Buffer).ConfigureAwait(false);
        }
    }

    private sealed class NetworkStreamAdapter : IStream
    {
        private readonly NetworkStream _stream;

        public NetworkStreamAdapter(NetworkStream stream)
        {
            _stream = stream;
        }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _stream.ReadAsync(buffer, cancellationToken);
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _stream.WriteAsync(buffer, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            _stream.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
