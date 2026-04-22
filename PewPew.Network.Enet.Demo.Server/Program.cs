using System;
using System.Text;
using PewPew.Network.Enet;

// ─────────────────────────────────────────────────────────────────────────────
// PewPew.Network.Enet — Demo Server
//
// Listens on 127.0.0.1:7777, echoes every received packet back to the sender.
// Run this first, then start PewPew.Network.Enet.Demo.Client.
// ─────────────────────────────────────────────────────────────────────────────

const string Host_IP   = "127.0.0.1";
const ushort Host_Port = 7777;
const int    MaxPeers  = 32;
const int    Channels  = 2;
const int    ServiceTimeoutMs = 15;

Console.Title = "ENet Demo — Server";
Console.WriteLine("=== ENet Demo Server ===");
Console.WriteLine($"Binding to {Host_IP}:{Host_Port}  (Ctrl+C to quit)");
Console.WriteLine();

Library.Initialize();

bool running = true;
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    running = false;
};

var address = new Address();
address.SetIP(Host_IP);
address.Port = Host_Port;

using var host = new Host();
host.Create(address, MaxPeers, Channels);

Console.WriteLine($"[Server] Listening...");

while (running)
{
    while (host.Service(ServiceTimeoutMs, out Event evt) > 0)
    {
        switch (evt.Type)
        {
            case EventType.Connect:
                Console.WriteLine($"[Server] Client connected  — {evt.Peer.IP}:{evt.Peer.Port}  (id={evt.Peer.ID})");
                break;

            case EventType.Receive:
            {
                var pkt = evt.Packet;
                int len = pkt.Length;
                byte[] buf = new byte[len];
                pkt.CopyTo(buf);
                string msg = Encoding.UTF8.GetString(buf, 0, len);

                Console.WriteLine($"[Server] Received ch={evt.ChannelID}  \"{msg}\"  from peer {evt.Peer.ID}");

                // Echo the message back
                var reply = new Packet();
                reply.Create(buf, len, PacketFlags.Reliable);
                var peer = evt.Peer;
                peer.Send(1, ref reply);

                pkt.Dispose();
                break;
            }

            case EventType.Disconnect:
            case EventType.Timeout:
                Console.WriteLine($"[Server] Client disconnected — peer {evt.Peer.ID}  (type={evt.Type})");
                break;
        }
    }
}

Console.WriteLine("[Server] Shutting down...");
host.Flush();
Library.Deinitialize();
Console.WriteLine("[Server] Done.");
