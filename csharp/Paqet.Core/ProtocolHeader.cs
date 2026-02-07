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
}
