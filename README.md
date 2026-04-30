# PewPew.Network.Enet

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Status: Alpha](https://img.shields.io/badge/Status-Alpha-orange.svg)]()

> ⚠️ **This project is in alpha stage and is not production-ready.** There may be bugs. Use at your own risk.

A fully **managed (pure C#)** implementationof the [ENet](http://enet.bespin.org/) reliable UDP networking library, targeting **.NET 7.0**.

Designed as a drop-in API-compatible replacement for [ENet-CSharp (SoftwareGuy)](https://github.com/nxrighthere/ENet-CSharp) — no native `enet.dll` required.

---

## Features

- **Pure managed C#** — no native DLL, no P/Invoke, no platform-specific binaries
- **IPv4 and IPv6** support (IPv4 addresses are normalized to IPv4-mapped IPv6 internally)
- **Multiple channels** per connection (up to 255)
- **Flexible packet delivery** flags:
  - `Reliable` — guaranteed, ordered delivery
  - `Unsequenced` — unreliable, unordered
  - `UnreliableFragmented` — unreliable with automatic fragmentation
  - `Instant` — bypass sequencing entirely
  - `Unthrottled` — exempt from throttle
  - `NoAllocate` — zero-copy send from existing buffer
- **Bandwidth limiting** (incoming/outgoing) per host
- **Packet throttle control** — adaptive congestion control per peer
- **Broadcast** — to all peers, to all except one, or to a specific subset
- **CRC64 checksum hook** — pluggable checksum callback on the host
- **Duplicate peer limiting** per host
- **Comprehensive test suite**

---

## Requirements

- [.NET 7.0 SDK](https://dotnet.microsoft.com/download/dotnet/7.0) or later

---

## Quick Start

### Server

```csharp
using PewPew.Network.Enet;

Library.Initialize();

using (Host server = new Host())
{
    Address address = new Address();
    address.Port = 44556;
    server.Create(address, peerLimit: 100, channelLimit: 4);

    Event netEvent;

    while (true)
    {
        bool polled = false;
        while (!polled)
        {
            if (server.CheckEvents(out netEvent) <= 0)
            {
                if (server.Service(timeout: 15, out netEvent) <= 0)
                    break;
                polled = true;
            }

            switch (netEvent.Type)
            {
                case EventType.Connect:
                    Console.WriteLine($"Client connected — ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}");
                    break;

                case EventType.Disconnect:
                    Console.WriteLine($"Client disconnected — ID: {netEvent.Peer.ID}");
                    break;

                case EventType.Timeout:
                    Console.WriteLine($"Client timeout — ID: {netEvent.Peer.ID}");
                    break;

                case EventType.Receive:
                    Console.WriteLine($"Received {netEvent.Packet.Length} bytes on channel {netEvent.ChannelID}");
                    netEvent.Packet.Dispose();
                    break;
            }
        }
    }

    server.Flush();
}

Library.Deinitialize();
```

### Client

```csharp
using PewPew.Network.Enet;

Library.Initialize();

using (Host client = new Host())
{
    Address address = new Address();
    address.SetHost("127.0.0.1");
    address.Port = 44556;
    client.Create();

    Peer server = client.Connect(address, channelLimit: 4);

    Event netEvent;
    bool connected = false;

    while (true)
    {
        bool polled = false;
        while (!polled)
        {
            if (client.CheckEvents(out netEvent) <= 0)
            {
                if (client.Service(timeout: 15, out netEvent) <= 0)
                    break;
                polled = true;
            }

            switch (netEvent.Type)
            {
                case EventType.Connect:
                    Console.WriteLine("Connected to server");
                    connected = true;
                    break;

                case EventType.Disconnect:
                case EventType.Timeout:
                    Console.WriteLine("Disconnected");
                    connected = false;
                    break;

                case EventType.Receive:
                    netEvent.Packet.Dispose();
                    break;
            }
        }

        if (connected)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes("Hello, ENet!");
            Packet packet = default;
            packet.Create(data, PacketFlags.Reliable);
            server.Send(channelID: 0, ref packet);
        }
    }

    client.Flush();
}

Library.Deinitialize();
```

---

## API Overview


### `Host` — key methods

| Method | Description |
|--------|-------------|
| `Create(address, peerLimit, channelLimit)` | Start listening (server) or create a client host |
| `Connect(address, channelLimit, data)` | Initiate connection to a remote host |
| `Service(timeout, out event)` | Dispatch events, wait up to `timeout` ms |
| `CheckEvents(out event)` | Non-blocking event poll |
| `Broadcast(channelID, ref packet)` | Send to all connected peers |
| `Flush()` | Force-send buffered outgoing packets |
| `SetBandwidthLimit(in, out)` | Cap incoming/outgoing bandwidth (bytes/s) |
| `SetChannelLimit(limit)` | Change channel limit after creation |
| `SetChecksumCallback(fn)` | Plug in a custom checksum function (e.g. CRC64) |

### `Peer` — key methods

| Method | Description |
|--------|-------------|
| `Send(channelID, ref packet)` | Send a packet to this peer |
| `Disconnect(data)` | Graceful disconnect |
| `DisconnectNow(data)` | Immediate disconnect |
| `DisconnectLater(data)` | Disconnect after all queued packets are sent |
| `Ping()` | Send a ping |
| `Timeout(limit, min, max)` | Configure timeout parameters |
| `ConfigureThrottle(interval, accel, decel, threshold)` | Fine-tune bandwidth throttle |
| `Reset()` | Hard reset the peer |

---

## Project Structure

```
PewPew.Network.Enet.sln
│
├── PewPew.Network.Enet/             # Core library (pure managed ENet)
├── PewPew.Network.Enet.Tests/       # Unit & integration tests
│
├── PewPew.Network.Enet.Demo.Server/ # Demo server using the managed library
├── PewPew.Network.Enet.Demo.Client/ # Demo client using the managed library
│
├── NativeEnet.Demo.Server/          # Reference demo server using native enet.dll
└── NativeEnet.Demo.Client/          # Reference demo client using native enet.dll
```

