using System.Diagnostics;
using Paqet.Core;

namespace Paqet.Socket;

public sealed class TcpPacketState
{
    private uint _seq;
    private uint _ack;
    private uint _ts;
    private readonly TcpFlagSequence _flags;

    public TcpPacketState(uint initialSeq = 1, uint initialAck = 0, TcpFlagSequence? flags = null)
    {
        _seq = initialSeq;
        _ack = initialAck;
        _ts = (uint)Stopwatch.GetTimestamp();
        _flags = flags ?? new TcpFlagSequence(new[] { TcpFlagPresets.PshAck });
    }

    public (uint Seq, uint Ack, uint Timestamp, TcpFlags Flags) Next(int payloadLength)
    {
        var flags = _flags.Next();
        var seq = _seq;
        var ack = _ack;
        if (flags.Syn)
        {
            seq = _seq;
            _seq += 1;
        }
        else
        {
            _seq += (uint)payloadLength;
        }
        _ts += 1;
        return (seq, ack, _ts, flags);
    }

    public void SetAck(uint ack)
    {
        _ack = ack;
    }
}
