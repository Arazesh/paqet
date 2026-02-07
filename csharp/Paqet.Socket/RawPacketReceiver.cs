using System.Net;
using System.Net.Sockets;

namespace Paqet.Socket;

public sealed class RawPacketReceiver : IDisposable
{
    private readonly System.Net.Sockets.Socket _socket;

    public RawPacketReceiver(IPAddress listenAddress)
    {
        _socket = new System.Net.Sockets.Socket(AddressFamily.InterNetwork, SocketType.Raw, System.Net.Sockets.ProtocolType.Tcp);
        _ = listenAddress;
    }

    public int Receive(Span<byte> buffer)
    {
        return _socket.Receive(buffer);
    }

    public void Dispose()
    {
        _socket.Dispose();
    }
}
