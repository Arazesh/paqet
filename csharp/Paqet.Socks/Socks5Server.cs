using System.Net;
using System.Net.Sockets;
using Paqet.Core;

namespace Paqet.Socks;

public sealed class Socks5Server
{
    private readonly TcpListener _listener;
    private readonly ITransport _transport;
    private readonly Address _serverAddress;

    public Socks5Server(IPEndPoint listenEndPoint, ITransport transport, Address serverAddress)
    {
        _listener = new TcpListener(listenEndPoint);
        _transport = transport;
        _serverAddress = serverAddress;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _listener.Start();
        while (!cancellationToken.IsCancellationRequested)
        {
            var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            _ = HandleClientAsync(client, cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var _ = client;
        var stream = client.GetStream();
        var header = new byte[2];
        await StreamHelpers.ReadExactAsync(new NetworkStreamAdapter(stream), header, cancellationToken).ConfigureAwait(false);
        if (header[0] != 0x05)
        {
            return;
        }
        var methods = new byte[header[1]];
        await StreamHelpers.ReadExactAsync(new NetworkStreamAdapter(stream), methods, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(new byte[] { 0x05, 0x00 }, cancellationToken).ConfigureAwait(false);

        var request = await ReadRequestAsync(stream, cancellationToken).ConfigureAwait(false);
        if (request.Command == 0x01)
        {
            await HandleConnectAsync(stream, request, cancellationToken).ConfigureAwait(false);
        }
        else if (request.Command == 0x03)
        {
            await HandleUdpAssociateAsync(stream, request, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await SendReplyAsync(stream, 0x07, request, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleConnectAsync(NetworkStream stream, SocksRequest request, CancellationToken cancellationToken)
    {
        await SendReplyAsync(stream, 0x00, request, cancellationToken).ConfigureAwait(false);
        await using var connection = await _transport.DialAsync(_serverAddress, cancellationToken).ConfigureAwait(false);
        await using var tunnel = await connection.OpenStreamAsync(cancellationToken).ConfigureAwait(false);
        await ProtocolHeader.WriteAsync(tunnel, ProtocolHeader.ForTcpFlags(new[] { TcpFlagPresets.PshAck }), cancellationToken).ConfigureAwait(false);
        await ProtocolHeader.WriteAsync(tunnel, ProtocolHeader.ForTcp(request.Address), cancellationToken).ConfigureAwait(false);

        var clientStream = new NetworkStreamAdapter(stream);
        var copyToServer = StreamCopy.CopyAsync(clientStream, tunnel, cancellationToken: cancellationToken);
        var copyToClient = StreamCopy.CopyAsync(tunnel, clientStream, cancellationToken: cancellationToken);
        await Task.WhenAny(copyToServer, copyToClient).ConfigureAwait(false);
    }

    private async Task HandleUdpAssociateAsync(NetworkStream stream, SocksRequest request, CancellationToken cancellationToken)
    {
        using var udpClient = new UdpClient(0);
        var local = (IPEndPoint)udpClient.Client.LocalEndPoint!;
        await SendReplyAsync(stream, 0x00, request with { BoundAddress = new Address(local.Address.ToString(), local.Port) }, cancellationToken)
            .ConfigureAwait(false);

        await using var connection = await _transport.DialAsync(_serverAddress, cancellationToken).ConfigureAwait(false);
        await using var tunnel = await connection.OpenStreamAsync(cancellationToken).ConfigureAwait(false);
        await ProtocolHeader.WriteAsync(tunnel, ProtocolHeader.ForUdp(new Address(request.Address.Host, request.Address.Port)), cancellationToken)
            .ConfigureAwait(false);

        var clientStream = new NetworkStreamAdapter(stream);
        var udpToServer = RelayUdpToServerAsync(udpClient, tunnel, cancellationToken);
        var serverToUdp = RelayServerToUdpAsync(udpClient, tunnel, cancellationToken);
        var tcpHold = HoldTcpAsync(clientStream, cancellationToken);
        await Task.WhenAny(udpToServer, serverToUdp, tcpHold).ConfigureAwait(false);
    }

    private static async Task RelayUdpToServerAsync(UdpClient udpClient, IStream tunnel, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await udpClient.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            var frame = UdpFrame.FromSocksDatagram(result.Buffer);
            var payload = frame.Serialize();
            await tunnel.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task RelayServerToUdpAsync(UdpClient udpClient, IStream tunnel, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var frame = await UdpFrame.ReadAsync(tunnel, cancellationToken).ConfigureAwait(false);
            var datagram = frame.ToSocksDatagram();
            await udpClient.SendAsync(datagram, datagram.Length, new IPEndPoint(IPAddress.Parse(frame.Address.Host), frame.Address.Port))
                .ConfigureAwait(false);
        }
    }

    private static async Task HoldTcpAsync(IStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
        }
    }

    private static async Task<SocksRequest> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        var atyp = header[3];
        string host;
        if (atyp == 0x01)
        {
            var addr = new byte[4];
            await stream.ReadExactlyAsync(addr, cancellationToken).ConfigureAwait(false);
            host = new IPAddress(addr).ToString();
        }
        else if (atyp == 0x03)
        {
            var length = stream.ReadByte();
            var name = new byte[length];
            await stream.ReadExactlyAsync(name, cancellationToken).ConfigureAwait(false);
            host = System.Text.Encoding.ASCII.GetString(name);
        }
        else if (atyp == 0x04)
        {
            var addr = new byte[16];
            await stream.ReadExactlyAsync(addr, cancellationToken).ConfigureAwait(false);
            host = new IPAddress(addr).ToString();
        }
        else
        {
            throw new InvalidOperationException("Unsupported address type.");
        }
        var portBytes = new byte[2];
        await stream.ReadExactlyAsync(portBytes, cancellationToken).ConfigureAwait(false);
        var port = (portBytes[0] << 8) | portBytes[1];
        return new SocksRequest(header[1], new Address(host, port), null);
    }

    private static async Task SendReplyAsync(NetworkStream stream, byte status, SocksRequest request, CancellationToken cancellationToken)
    {
        var bound = request.BoundAddress ?? request.Address;
        var reply = new List<byte> { 0x05, status, 0x00 };
        if (IPAddress.TryParse(bound.Host, out var ip))
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                reply.Add(0x01);
                reply.AddRange(ip.GetAddressBytes());
            }
            else
            {
                reply.Add(0x04);
                reply.AddRange(ip.GetAddressBytes());
            }
        }
        else
        {
            var hostBytes = System.Text.Encoding.ASCII.GetBytes(bound.Host);
            reply.Add(0x03);
            reply.Add((byte)hostBytes.Length);
            reply.AddRange(hostBytes);
        }
        reply.Add((byte)(bound.Port >> 8));
        reply.Add((byte)(bound.Port & 0xff));
        await stream.WriteAsync(reply.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    private sealed record SocksRequest(byte Command, Address Address, Address? BoundAddress);

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
            return ValueTask.CompletedTask;
        }
    }
}
