using Paqet.Core;
using Paqet.Transport.Quic;

namespace Paqet.Transport.Kcp;

public sealed class KcpTransport : ITransport
{
    private readonly QuicTransport _fallback = new();

    public ValueTask<IConnection> DialAsync(Address address, CancellationToken cancellationToken = default)
    {
        return _fallback.DialAsync(address, cancellationToken);
    }

    public ValueTask<IListener> ListenAsync(Address address, CancellationToken cancellationToken = default)
    {
        return _fallback.ListenAsync(address, cancellationToken);
    }
}
