using System.Net;
using System.Net.NetworkInformation;
using PacketDotNet;
using SharpPcap;
using Paqet.Core;

namespace Paqet.Socket;

public sealed class EthernetPacketSender : IDisposable
{
    private readonly IInjectionDevice _device;
    private readonly PhysicalAddress _sourceMac;
    private readonly PhysicalAddress _gatewayMac;
    private readonly IPAddress _sourceAddress;

    public EthernetPacketSender(string deviceName, IPAddress sourceAddress, PhysicalAddress sourceMac, PhysicalAddress gatewayMac)
    {
        _device = CaptureDeviceList.Instance.FirstOrDefault(d => d.Name == deviceName) as IInjectionDevice
                  ?? throw new InvalidOperationException($"Capture device not found: {deviceName}");
        _sourceAddress = sourceAddress;
        _sourceMac = sourceMac;
        _gatewayMac = gatewayMac;
        _device.Open(DeviceModes.Promiscuous, read_timeout: 1);
    }

    public void Send(IPAddress destination, ushort sourcePort, ushort destPort, TcpFlags flags, uint seq, uint ack, ReadOnlySpan<byte> payload)
    {
        var tcp = new TcpPacket(sourcePort, destPort)
        {
            SequenceNumber = seq,
            AcknowledgmentNumber = ack,
            WindowSize = 65535
        };
        tcp.HeaderData[13] = BuildFlags(flags);
        tcp.PayloadData = payload.ToArray();

        var ip = new IPv4Packet(_sourceAddress, destination)
        {
            TimeToLive = 64,
            Protocol = PacketDotNet.ProtocolType.Tcp
        };
        ip.PayloadPacket = tcp;

        var eth = new EthernetPacket(_sourceMac, _gatewayMac, EthernetType.IPv4)
        {
            PayloadPacket = ip
        };

        tcp.UpdateTcpChecksum(ip);
        ip.UpdateCalculatedValues();
        eth.UpdateCalculatedValues();

        _device.SendPacket(eth);
    }

    public void Send(IPAddress destination, ushort sourcePort, ushort destPort, TcpPacketState state, ReadOnlySpan<byte> payload)
    {
        var (seq, ack, _, flags) = state.Next(payload.Length);
        Send(destination, sourcePort, destPort, flags, seq, ack, payload);
    }

    public void Dispose()
    {
        _device.Close();
    }

    private static byte BuildFlags(TcpFlags flags)
    {
        byte value = 0;
        if (flags.Fin) value |= 0x01;
        if (flags.Syn) value |= 0x02;
        if (flags.Rst) value |= 0x04;
        if (flags.Psh) value |= 0x08;
        if (flags.Ack) value |= 0x10;
        if (flags.Urg) value |= 0x20;
        if (flags.Ece) value |= 0x40;
        if (flags.Cwr) value |= 0x80;
        return value;
    }
}
