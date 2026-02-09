# Paqet C# Port (Scaffold)

This folder contains the initial scaffold for a C# port of Paqet.

## Target
- .NET 10 (works on Windows/Linux)
- Raw packet manipulation via SharpPcap/Npcap/libpcap
- KCP transport preferred; QUIC fallback acceptable

## Projects
- `Paqet.Core` — protocol header, config models, streams, copy utilities
- `Paqet.Transport.Kcp` — KCP + stream mux adapter
- `Paqet.Transport.Quic` — QUIC fallback adapter
- `Paqet.Socks` — SOCKS5 server/handlers
- `Paqet.Client` — client runner
- `Paqet.Server` — server runner

## Status
- Core abstractions are present and usable.
- KCP transport is implemented using the KcpSharp library; QUIC is available as an alternative.
- Raw packet sender/receiver are available (requires elevated privileges).
- `EthernetPacketSender` builds Ethernet frames using a gateway MAC via SharpPcap.

## Usage
```
Paqet.Server <listenHost:port>

Paqet.Client socks <listenHost:port> <serverHost:port>
Paqet.Client forward <tcp|udp> <listenHost:port> <targetHost:port> <serverHost:port>
```

## Windows raw packet injection (Npcap)

Windows blocks crafted TCP packets via raw sockets, so the C# port uses SharpPcap/Npcap injection instead. Configure these environment variables before running the client/server:

- `PAQET_PCAP_DEVICE`: Npcap device name (format: `\\Device\\NPF_{GUID}`).
  - PowerShell: `Get-NetAdapter | Select-Object Name, InterfaceGuid`
  - Use the active adapter's `InterfaceGuid` and format it as `\\Device\\NPF_{GUID}`.
- `PAQET_SOURCE_MAC`: Source interface MAC address (e.g. `aa:bb:cc:dd:ee:ff`).
  - PowerShell: `Get-NetAdapter | Select-Object Name, MacAddress`
- `PAQET_GATEWAY_MAC`: Default gateway/router MAC address.
  - PowerShell: `Get-NetRoute -DestinationPrefix 0.0.0.0/0 | Select-Object NextHop`
  - Then: `arp -a <gateway_ip>` and copy the MAC address.
