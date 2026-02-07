using Paqet.Core;

namespace Paqet.Transport.Quic;

public sealed class QuicTransport : ITransport
{
    private readonly TcpTransport _fallback = new();

    public ValueTask<IConnection> DialAsync(Address address, CancellationToken cancellationToken = default)
    {
        return _fallback.DialAsync(address, cancellationToken);
    }

    public ValueTask<IListener> ListenAsync(Address address, CancellationToken cancellationToken = default)
    {
        return _fallback.ListenAsync(address, cancellationToken);
    }
}
