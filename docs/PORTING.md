# C# Port Plan (Paqet)

This document describes how to implement Paqet in C# with feature parity (not a 1:1 port), targeting .NET 10 and Windows/Linux.

## Goals
- **Client + Server** roles with configuration parity.
- **SOCKS5 proxy** (TCP + UDP associate).
- **Raw packet transport** with customizable TCP flags, seq/ack, timestamps, and header manipulation.
- **Multiplexed streams** over a reliable transport (prefer KCP; QUIC fallback allowed).
- **Forwarding mode** (local → remote TCP/UDP).

## Proposed C# Architecture

```
Paqet.Core
  - Configuration models
  - Address parsing / serialization
  - ProtocolHeader (PTCP/PTCPF/PUDP)
  - Stream + Connection abstractions
  - Copy helpers

Paqet.Transport.Kcp
  - KCP session wrapper
  - Smux (or equivalent) stream multiplexer

Paqet.Transport.Quic (fallback)
  - MsQuic-based transport

Paqet.Socket
  - Raw packet sender + receiver
  - TCP header builder (flags/seq/ts)
  - PacketConn (net.PacketConn equivalent)

Paqet.Socks
  - SOCKS5 TCP/UDP handlers
  - Bidirectional copy loops

Paqet.Client
  - Client runner
  - SOCKS and forward startup

Paqet.Server
  - Server listener
  - Protocol dispatch
```

## Feature Mapping (Go → C#)

### SOCKS
- `internal/socks/tcp_handle.go` → `Paqet.Socks.TcpHandler`
- `internal/socks/udp_handle.go` → `Paqet.Socks.UdpHandler`

### Protocol header (PTCP/PTCPF/PUDP)
- `internal/protocol/protocol.go` → `Paqet.Core.ProtocolHeader`
  - C# binary encoding (e.g., Span-based) instead of Gob.

### Transport + Streams
- `internal/tnet/kcp` → `Paqet.Transport.Kcp`
  - Use a C# KCP library (e.g., `KcpSharp`, `kcp2k`, or custom).
  - Multiplexing: `smux` equivalent (if unavailable, use a custom stream mux).

### Raw Packet Sender
- `internal/socket/send_handle.go` → `Paqet.Socket.RawSender`
  - Uses SharpPcap/Npcap on Windows and libpcap on Linux.
  - Implements TCP header manipulation (PSH/ACK, seq/ack, timestamps, options).

## Build + Runtime Dependencies
- **Windows**: Npcap + SharpPcap.
- **Linux**: libpcap + SharpPcap.
- **KCP**: managed library; if unavailable, fall back to QUIC (`MsQuic`).

## Current Scaffold State
- `Paqet.Transport.Quic` is implemented using `System.Net.Quic` with self-signed certificates.
- `Paqet.Transport.Kcp` currently delegates to QUIC until a managed KCP library is integrated.

## Initial Implementation Steps
1. **Core types**: Address, ProtocolHeader, config parsing.
2. **Transport**: pick KCP lib or QUIC; implement stream mux.
3. **Socket layer**: implement raw sender/receiver with packet capture.
4. **SOCKS**: TCP connect + UDP associate.
5. **Server dispatch**: accept streams, read ProtocolHeader, dial target.

## Notes
- Use **binary encoding** for ProtocolHeader (avoid Gob).
- Keep interfaces clean to allow switching KCP ↔ QUIC.
- Keep raw packet layer isolated to avoid platform-specific leakage into core logic.
