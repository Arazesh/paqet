using System.Net;
using System.Net.Sockets;

namespace Paqet.Core;

public sealed class TcpTransport : ITransport
{
    public async ValueTask<IConnection> DialAsync(Address address, CancellationToken cancellationToken = default)
    {
        var client = new TcpClient();
        await client.ConnectAsync(address.Host, address.Port, cancellationToken).ConfigureAwait(false);
        return new TcpConnection(client, address);
    }

    public ValueTask<IListener> ListenAsync(Address address, CancellationToken cancellationToken = default)
    {
        var listener = new TcpListener(address.ResolveIPAddress(), address.Port);
        listener.Start();
        return ValueTask.FromResult<IListener>(new TcpListenerAdapter(listener));
    }

    private sealed class TcpListenerAdapter : IListener
    {
        private readonly TcpListener _listener;

        public TcpListenerAdapter(TcpListener listener)
        {
            _listener = listener;
        }

        public async ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken = default)
        {
            var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            var endPoint = (IPEndPoint)client.Client.RemoteEndPoint!;
            var address = new Address(endPoint.Address.ToString(), endPoint.Port);
            return new TcpConnection(client, address);
        }

        public ValueTask DisposeAsync()
        {
            _listener.Stop();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TcpConnection : IConnection
    {
        private readonly TcpClient _client;
        private readonly Address _remote;
        private readonly TcpStream _stream;
        private int _streamClaimed;

        public TcpConnection(TcpClient client, Address remote)
        {
            _client = client;
            _remote = remote;
            _stream = new TcpStream(client.GetStream());
        }

        public ValueTask<IStream> OpenStreamAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ClaimStream());
        }

        public ValueTask<IStream> AcceptStreamAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ClaimStream());
        }

        private IStream ClaimStream()
        {
            if (Interlocked.Exchange(ref _streamClaimed, 1) == 1)
            {
                throw new InvalidOperationException("TCP transport supports only a single stream per connection.");
            }
            return _stream;
        }

        public ValueTask DisposeAsync()
        {
            _client.Close();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TcpStream : IStream
    {
        private readonly NetworkStream _stream;

        public TcpStream(NetworkStream stream)
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
