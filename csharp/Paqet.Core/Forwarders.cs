using System.Net;
using System.Net.Sockets;

namespace Paqet.Core;

public static class Forwarders
{
    public static async Task RunTcpForwardAsync(Address listen, Address target, Address server, ITransport transport, CancellationToken cancellationToken = default)
    {
        var listener = new TcpListener(listen.ResolveIPAddress(), listen.Port);
        listener.Start();
        while (!cancellationToken.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            _ = HandleTcpClientAsync(client, target, server, transport, cancellationToken);
        }
    }

    private static async Task HandleTcpClientAsync(TcpClient client, Address target, Address server, ITransport transport, CancellationToken cancellationToken)
    {
        using var _ = client;
        await using var connection = await transport.DialAsync(server, cancellationToken).ConfigureAwait(false);
        await using var tunnel = await connection.OpenStreamAsync(cancellationToken).ConfigureAwait(false);
        await ProtocolHeader.WriteAsync(tunnel, ProtocolHeader.ForTcpFlags(new[] { TcpFlagPresets.PshAck }), cancellationToken).ConfigureAwait(false);
        await ProtocolHeader.WriteAsync(tunnel, ProtocolHeader.ForTcp(target), cancellationToken).ConfigureAwait(false);

        var network = client.GetStream();
        var clientStream = new NetworkStreamAdapter(network);
        var copyToServer = StreamCopy.CopyAsync(clientStream, tunnel, cancellationToken: cancellationToken);
        var copyToClient = StreamCopy.CopyAsync(tunnel, clientStream, cancellationToken: cancellationToken);
        await Task.WhenAny(copyToServer, copyToClient).ConfigureAwait(false);
    }

    public static async Task RunUdpForwardAsync(Address listen, Address target, Address server, ITransport transport, CancellationToken cancellationToken = default)
    {
        using var udp = new UdpClient(new IPEndPoint(listen.ResolveIPAddress(), listen.Port));
        var tunnels = new Dictionary<string, UdpForwardTunnel>();
        var fromClient = RelayUdpToTunnelAsync(udp, target, server, transport, tunnels, cancellationToken);
        await fromClient.ConfigureAwait(false);
    }

    private static async Task RelayUdpToTunnelAsync(
        UdpClient udp,
        Address target,
        Address server,
        ITransport transport,
        Dictionary<string, UdpForwardTunnel> tunnels,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await udp.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            var key = result.RemoteEndPoint.ToString();
            if (!tunnels.TryGetValue(key, out var tunnel))
            {
                tunnel = await CreateForwardTunnelAsync(udp, result.RemoteEndPoint, target, server, transport, cancellationToken)
                    .ConfigureAwait(false);
                tunnels[key] = tunnel;
            }

            await tunnel.Stream.WriteAsync(result.Buffer, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<UdpForwardTunnel> CreateForwardTunnelAsync(
        UdpClient udp,
        IPEndPoint clientEndPoint,
        Address target,
        Address server,
        ITransport transport,
        CancellationToken cancellationToken)
    {
        var connection = await transport.DialAsync(server, cancellationToken).ConfigureAwait(false);
        var stream = await connection.OpenStreamAsync(cancellationToken).ConfigureAwait(false);
        await ProtocolHeader.WriteAsync(stream, ProtocolHeader.ForUdp(target), cancellationToken).ConfigureAwait(false);
        _ = Task.Run(() => RelayTunnelToUdpAsync(udp, stream, clientEndPoint, connection, cancellationToken), cancellationToken);
        return new UdpForwardTunnel(connection, stream, clientEndPoint);
    }

    private static async Task RelayTunnelToUdpAsync(
        UdpClient udp,
        IStream tunnel,
        IPEndPoint clientEndPoint,
        IConnection connection,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1500];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await tunnel.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                await udp.SendAsync(buffer.AsMemory(0, read), clientEndPoint, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await tunnel.DisposeAsync().ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed record UdpForwardTunnel(IConnection Connection, IStream Stream, IPEndPoint ClientEndPoint);

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
