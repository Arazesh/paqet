namespace Paqet.Core;

public sealed record TcpFlags(
    bool Fin,
    bool Syn,
    bool Rst,
    bool Psh,
    bool Ack,
    bool Urg,
    bool Ece,
    bool Cwr,
    bool Ns
);

public static class TcpFlagPresets
{
    public static readonly TcpFlags PshAck = new(false, false, false, true, true, false, false, false, false);
}

public sealed class TcpFlagSequence
{
    private readonly IReadOnlyList<TcpFlags> _flags;
    private int _index;

    public TcpFlagSequence(IReadOnlyList<TcpFlags> flags)
    {
        _flags = flags.Count == 0 ? new[] { TcpFlagPresets.PshAck } : flags;
    }

    public TcpFlags Next()
    {
        var idx = Interlocked.Increment(ref _index);
        return _flags[idx % _flags.Count];
    }
}
