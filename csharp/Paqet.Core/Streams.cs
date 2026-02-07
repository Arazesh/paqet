namespace Paqet.Core;

public interface IStream : IAsyncDisposable
{
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default);
}

public interface IConnection : IAsyncDisposable
{
    ValueTask<IStream> OpenStreamAsync(CancellationToken cancellationToken = default);
    ValueTask<IStream> AcceptStreamAsync(CancellationToken cancellationToken = default);
}

public interface ITransport
{
    ValueTask<IConnection> DialAsync(Address address, CancellationToken cancellationToken = default);
    ValueTask<IListener> ListenAsync(Address address, CancellationToken cancellationToken = default);
}

public interface IListener : IAsyncDisposable
{
    ValueTask<IConnection> AcceptAsync(CancellationToken cancellationToken = default);
}
