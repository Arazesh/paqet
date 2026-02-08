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

    public byte[] ToSocksDatagram()
    {
        var buffer = new byte[4 + 1 + 255 + 2 + Payload.Length];
        buffer[0] = 0x00;
        buffer[1] = 0x00;
        buffer[2] = 0x00;
        var offset = 3;
        if (System.Net.IPAddress.TryParse(Address.Host, out var ip))
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                buffer[offset++] = 0x01;
                ip.GetAddressBytes().CopyTo(buffer.AsSpan(offset));
                offset += 4;
            }
            else
            {
                buffer[offset++] = 0x04;
                ip.GetAddressBytes().CopyTo(buffer.AsSpan(offset));
                offset += 16;
            }
        }
        else
        {
            var hostBytes = System.Text.Encoding.ASCII.GetBytes(Address.Host);
            buffer[offset++] = 0x03;
            buffer[offset++] = (byte)hostBytes.Length;
            hostBytes.CopyTo(buffer.AsSpan(offset));
            offset += hostBytes.Length;
        }

        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset, 2), (ushort)Address.Port);
        offset += 2;
        Payload.Span.CopyTo(buffer.AsSpan(offset));
        return buffer.AsSpan(0, offset + Payload.Length).ToArray();
    }
}
