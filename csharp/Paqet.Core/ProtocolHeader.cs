namespace Paqet.Core;

public enum ProtocolType : byte
{
    Ping = 0x01,
    Pong = 0x02,
    TcpFlags = 0x03,
    Tcp = 0x04,
    Udp = 0x05,
}

public sealed record ProtocolHeader(ProtocolType Type, Address? Address, IReadOnlyList<TcpFlags>? Flags)
{
    public static ProtocolHeader ForTcp(Address address) => new(ProtocolType.Tcp, address, null);
    public static ProtocolHeader ForUdp(Address address) => new(ProtocolType.Udp, address, null);
    public static ProtocolHeader ForTcpFlags(IReadOnlyList<TcpFlags> flags) => new(ProtocolType.TcpFlags, null, flags);

    public static async ValueTask WriteAsync(IStream stream, ProtocolHeader header, CancellationToken cancellationToken = default)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write((byte)header.Type);
        if (header.Type == ProtocolType.Tcp || header.Type == ProtocolType.Udp)
        {
            if (header.Address is null)
            {
                throw new InvalidOperationException("ProtocolHeader requires an address for TCP/UDP.");
            }
            writer.Write(header.Address.Host);
            writer.Write(header.Address.Port);
        }
        else if (header.Type == ProtocolType.TcpFlags)
        {
            var flags = header.Flags ?? Array.Empty<TcpFlags>();
            writer.Write(flags.Count);
            foreach (var flag in flags)
            {
                writer.Write(flag.Fin);
                writer.Write(flag.Syn);
                writer.Write(flag.Rst);
                writer.Write(flag.Psh);
                writer.Write(flag.Ack);
                writer.Write(flag.Urg);
                writer.Write(flag.Ece);
                writer.Write(flag.Cwr);
                writer.Write(flag.Ns);
            }
        }
        writer.Flush();
        var data = ms.ToArray();
        var lengthPrefix = BitConverter.GetBytes((ushort)data.Length);
        await stream.WriteAsync(lengthPrefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<ProtocolHeader> ReadAsync(IStream stream, CancellationToken cancellationToken = default)
    {
        var prefix = new byte[2];
        await StreamHelpers.ReadExactAsync(stream, prefix, cancellationToken).ConfigureAwait(false);
        var length = BitConverter.ToUInt16(prefix, 0);
        var payload = new byte[length];
        await StreamHelpers.ReadExactAsync(stream, payload, cancellationToken).ConfigureAwait(false);

        using var ms = new MemoryStream(payload);
        using var reader = new BinaryReader(ms, System.Text.Encoding.UTF8, leaveOpen: true);
        var type = (ProtocolType)reader.ReadByte();
        if (type == ProtocolType.Tcp || type == ProtocolType.Udp)
        {
            var host = reader.ReadString();
            var port = reader.ReadInt32();
            return new ProtocolHeader(type, new Address(host, port), null);
        }
        if (type == ProtocolType.TcpFlags)
        {
            var count = reader.ReadInt32();
            var flags = new List<TcpFlags>(count);
            for (var i = 0; i < count; i++)
            {
                flags.Add(new TcpFlags(
                    reader.ReadBoolean(),
                    reader.ReadBoolean(),
                    reader.ReadBoolean(),
                    reader.ReadBoolean(),
                    reader.ReadBoolean(),
                    reader.ReadBoolean(),
                    reader.ReadBoolean(),
                    reader.ReadBoolean(),
                    reader.ReadBoolean()
                ));
            }
            return new ProtocolHeader(type, null, flags);
        }

        return new ProtocolHeader(type, null, null);
    }
}
