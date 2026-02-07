using System.Buffers.Binary;

namespace Paqet.Core;

public sealed record UdpFrame(Address Address, ReadOnlyMemory<byte> Payload)
{
    public static UdpFrame FromSocksDatagram(byte[] datagram)
    {
        if (datagram.Length < 4)
        {
            throw new InvalidOperationException("Invalid SOCKS UDP datagram.");
        }
        var atyp = datagram[3];
        var offset = 4;
        string host;
        if (atyp == 0x01)
        {
            host = new System.Net.IPAddress(datagram.AsSpan(offset, 4)).ToString();
            offset += 4;
        }
        else if (atyp == 0x03)
        {
            var length = datagram[offset++];
            host = System.Text.Encoding.ASCII.GetString(datagram, offset, length);
            offset += length;
        }
        else if (atyp == 0x04)
        {
            host = new System.Net.IPAddress(datagram.AsSpan(offset, 16)).ToString();
            offset += 16;
        }
        else
        {
            throw new InvalidOperationException("Unsupported SOCKS UDP ATYP.");
        }
        var port = BinaryPrimitives.ReadUInt16BigEndian(datagram.AsSpan(offset, 2));
        offset += 2;
        return new UdpFrame(new Address(host, port), datagram.AsMemory(offset));
    }

    public byte[] Serialize()
    {
        var hostBytes = System.Text.Encoding.UTF8.GetBytes(Address.Host);
        var length = 1 + 1 + 2 + hostBytes.Length + Payload.Length;
        var buffer = new byte[2 + length];
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(0, 2), (ushort)length);
        var offset = 2;
        buffer[offset++] = 0x03;
        buffer[offset++] = (byte)hostBytes.Length;
        hostBytes.CopyTo(buffer.AsSpan(offset));
        offset += hostBytes.Length;
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), (ushort)Address.Port);
        offset += 2;
        Payload.Span.CopyTo(buffer.AsSpan(offset));
        return buffer;
    }

    public static async ValueTask<UdpFrame> ReadAsync(IStream stream, CancellationToken cancellationToken = default)
    {
        var prefix = new byte[2];
        await StreamHelpers.ReadExactAsync(stream, prefix, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadUInt16BigEndian(prefix);
        var payload = new byte[length];
        await StreamHelpers.ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        var offset = 0;
        var atyp = payload[offset++];
        if (atyp != 0x03)
        {
            throw new InvalidOperationException("Unsupported UDP frame encoding.");
        }
        var hostLen = payload[offset++];
        var host = System.Text.Encoding.UTF8.GetString(payload, offset, hostLen);
        offset += hostLen;
        var port = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset, 2));
        offset += 2;
        return new UdpFrame(new Address(host, port), payload.AsMemory(offset));
    }

    public byte[] ToSocksDatagram()
    {
        var hostBytes = System.Text.Encoding.ASCII.GetBytes(Address.Host);
        var buffer = new byte[4 + 1 + hostBytes.Length + 2 + Payload.Length];
        buffer[0] = 0x00;
        buffer[1] = 0x00;
        buffer[2] = 0x00;
        buffer[3] = 0x03;
        buffer[4] = (byte)hostBytes.Length;
        hostBytes.CopyTo(buffer.AsSpan(5));
        var offset = 5 + hostBytes.Length;
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), (ushort)Address.Port);
        offset += 2;
        Payload.Span.CopyTo(buffer.AsSpan(offset));
        return buffer;
    }
}
