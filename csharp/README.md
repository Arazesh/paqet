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
- KCP/QUIC transports currently delegate to a TCP fallback to avoid NotImplementedException.
- Implementations are TODO; see `docs/PORTING.md` for the full plan.
