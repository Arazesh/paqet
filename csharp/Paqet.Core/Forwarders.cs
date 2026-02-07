using System.Net;
using System.Net.Sockets;

namespace Paqet.Core;

public static class Forwarders
{
    public static async Task RunTcpForwardAsync(Address listen, Address target, Address server, ITransport transport, CancellationToken cancellationToken = default)
    {
        var listener = new TcpListener(IPAddress.Parse(listen.Host), listen.Port);
        listener.Start();
        while (!cancellationToken.IsCancellationRequested)
        {
            var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            _ = HandleTcpClientAsync(client, target, server, transport, cancellationToken);
        }
    }

    private static async Task HandleTcpClientAsync(TcpClient client, Address target, Address server, ITransport transport, CancellationToken cancellationToken)
    {
        await using var _ = client.ConfigureAwait(false);
        await using var connection = await transport.DialAsync(server, cancellationToken).ConfigureAwait(false);
        await using var tunnel = await connection.OpenStreamAsync(cancellationToken).ConfigureAwait(false);
        await ProtocolHeader.WriteAsync(tunnel, ProtocolHeader.ForTcp(target), cancellationToken).ConfigureAwait(false);

        var network = client.GetStream();
        var clientStream = new NetworkStreamAdapter(network);
        var copyToServer = StreamCopy.CopyAsync(clientStream, tunnel, cancellationToken: cancellationToken);
        var copyToClient = StreamCopy.CopyAsync(tunnel, clientStream, cancellationToken: cancellationToken);
        await Task.WhenAny(copyToServer, copyToClient).ConfigureAwait(false);
    }

    public static async Task RunUdpForwardAsync(Address listen, Address target, Address server, ITransport transport, CancellationToken cancellationToken = default)
    {
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Parse(listen.Host), listen.Port));
        await using var connection = await transport.DialAsync(server, cancellationToken).ConfigureAwait(false);
        await using var tunnel = await connection.OpenStreamAsync(cancellationToken).ConfigureAwait(false);
        await ProtocolHeader.WriteAsync(tunnel, ProtocolHeader.ForUdp(target), cancellationToken).ConfigureAwait(false);

        var fromClient = RelayUdpToTunnelAsync(udp, tunnel, cancellationToken);
        var toClient = RelayTunnelToUdpAsync(udp, tunnel, cancellationToken);
        await Task.WhenAny(fromClient, toClient).ConfigureAwait(false);
    }

    private static async Task RelayUdpToTunnelAsync(UdpClient udp, IStream tunnel, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await udp.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            var frame = new UdpFrame(new Address(result.RemoteEndPoint.Address.ToString(), result.RemoteEndPoint.Port), result.Buffer);
            var data = frame.Serialize();
            await tunnel.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task RelayTunnelToUdpAsync(UdpClient udp, IStream tunnel, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var frame = await UdpFrame.ReadAsync(tunnel, cancellationToken).ConfigureAwait(false);
            await udp.SendAsync(frame.Payload.ToArray(), frame.Payload.Length, frame.Address.Host, frame.Address.Port, cancellationToken).ConfigureAwait(false);
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
