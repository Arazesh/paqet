using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Paqet.Core;

namespace Paqet.Transport.Quic;

public sealed class QuicTransport : ITransport
{
    private const string Alpn = "paqet";

    public async ValueTask<IConnection> DialAsync(Address address, CancellationToken cancellationToken = default)
    {
        var options = new QuicClientConnectionOptions
        {
            RemoteEndPoint = new DnsEndPoint(address.Host, address.Port),
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = new List<SslApplicationProtocol> { new(Alpn) },
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }
        };
        var connection = await QuicConnection.ConnectAsync(options, cancellationToken).ConfigureAwait(false);
        return new QuicConnectionAdapter(connection);
    }

    public async ValueTask<IListener> ListenAsync(Address address, CancellationToken cancellationToken = default)
    {
        var listenerOptions = new QuicListenerOptions
        {
            ListenEndPoint = new IPEndPoint(address.ResolveIPAddress(), address.Port),
            ApplicationProtocols = new List<SslApplicationProtocol> { new(Alpn) },
            ConnectionOptionsCallback = (_, _, _) =>
            {
                var cert = CreateSelfSignedCertificate();
                var options = new QuicServerConnectionOptions
                {
                    ServerAuthenticationOptions = new SslServerAuthenticationOptions
                    {
                        ApplicationProtocols = new List<SslApplicationProtocol> { new(Alpn) },
                        ServerCertificate = cert
                    }
                };
                return ValueTask.FromResult(options);
            }
        };
        var listener = await QuicListener.ListenAsync(listenerOptions, cancellationToken).ConfigureAwait(false);
        return new QuicListenerAdapter(listener);
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var ecdsa = ECDsa.Create();
        var request = new CertificateRequest("CN=Paqet", ecdsa, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    private sealed class QuicListenerAdapter : IListener
    {
        private readonly QuicListener _listener;

        public QuicListenerAdapter(QuicListener listener)
        {
            _listener = listener;
        }

        public async ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken = default)
        {
            var connection = await _listener.AcceptConnectionAsync(cancellationToken).ConfigureAwait(false);
            return new QuicConnectionAdapter(connection);
        }

        public async ValueTask DisposeAsync()
        {
            await _listener.DisposeAsync();
        }
    }

    private sealed class QuicConnectionAdapter : IConnection
    {
        private readonly QuicConnection _connection;

        public QuicConnectionAdapter(QuicConnection connection)
        {
            _connection = connection;
        }

        public async ValueTask<IStream> OpenStreamAsync(CancellationToken cancellationToken = default)
        {
            var stream = await _connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, cancellationToken).ConfigureAwait(false);
            return new QuicStreamAdapter(stream);
        }

        public async ValueTask<IStream> AcceptStreamAsync(CancellationToken cancellationToken = default)
        {
            var stream = await _connection.AcceptInboundStreamAsync(cancellationToken).ConfigureAwait(false);
            return new QuicStreamAdapter(stream);
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.CloseAsync(0).ConfigureAwait(false);
            await _connection.DisposeAsync();
        }
    }

    private sealed class QuicStreamAdapter : IStream
    {
        private readonly QuicStream _stream;

        public QuicStreamAdapter(QuicStream stream)
        {
            _stream = stream;
        }

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var result = await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            return result;
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
