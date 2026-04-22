using System;
using System.Text;
using System.Threading;
using PewPew.Network.Enet;

// ─────────────────────────────────────────────────────────────────────────────
// PewPew.Network.Enet — Demo Client
//
// Connects to 127.0.0.1:7777, sends "Hello #N" every second (5 times),
// prints each echo reply with peer RTT, then disconnects.
// Make sure PewPew.Network.Enet.Demo.Server is running first.
// ─────────────────────────────────────────────────────────────────────────────

const string Server_IP   = "127.0.0.1";
const ushort Server_Port = 7777;
const int    Channels    = 2;
const int    MessageCount = 5;
const int    ServiceTimeoutMs = 15;

Console.Title = "ENet Demo — Client";
Console.WriteLine("=== ENet Demo Client ===");
Console.WriteLine($"Connecting to {Server_IP}:{Server_Port}");
Console.WriteLine();

Library.Initialize();

var serverAddress = new Address();
serverAddress.SetIP(Server_IP);
serverAddress.Port = Server_Port;

using var host = new Host();
host.Create(null, 1, Channels);   // outgoing client, no bind address

Peer server = host.Connect(serverAddress, Channels);

// ── Wait for connection ───────────────────────────────────────────────────────
Console.Write("[Client] Waiting for connection...");

bool connected = false;
var connectDeadline = Environment.TickCount64 + 3_000;

while (Environment.TickCount64 < connectDeadline)
{
    if (host.Service(ServiceTimeoutMs, out Event evt) > 0 && evt.Type == EventType.Connect)
    {
        connected = true;
        break;
    }
}

if (!connected)
{
    Console.WriteLine(" TIMEOUT. Is the server running?");
    Library.Deinitialize();
    return;
}

Console.WriteLine(" Connected!");
Console.WriteLine();

// ── Send messages and receive echoes ─────────────────────────────────────────
int echosReceived = 0;

for (int i = 1; i <= MessageCount; i++)
{
    string text = $"Hello #{i}";
    byte[] data = Encoding.UTF8.GetBytes(text);

    var pkt = new Packet();
    pkt.Create(data, data.Length, PacketFlags.Reliable);
    server.Send(0, ref pkt);

    Console.WriteLine($"[Client] Sent: \"{text}\"");

    // Wait up to 2s for the echo
    var echoDeadline = Environment.TickCount64 + 2_000;
    bool gotEcho = false;

    while (Environment.TickCount64 < echoDeadline && !gotEcho)
    {
        while (host.Service(ServiceTimeoutMs, out Event evt) > 0)
        {
            if (evt.Type == EventType.Receive)
            {
                var p = evt.Packet;
                int len = p.Length;
                byte[] buf = new byte[len];
                p.CopyTo(buf);
                string reply = Encoding.UTF8.GetString(buf, 0, len);
                p.Dispose();

                Console.WriteLine($"[Client] Echo:  \"{reply}\"  (RTT={server.RoundTripTime}ms)");
                echosReceived++;
                gotEcho = true;
            }
            else if (evt.Type is EventType.Disconnect or EventType.Timeout)
            {
                Console.WriteLine("[Client] Disconnected by server.");
                goto done;
            }
        }
    }

    if (!gotEcho)
        Console.WriteLine($"[Client] No echo for message #{i} within timeout.");

    // Pause 1 second between messages (pump service while waiting)
    if (i < MessageCount)
    {
        var pauseEnd = Environment.TickCount64 + 1_000;
        while (Environment.TickCount64 < pauseEnd)
        {
            host.Service(ServiceTimeoutMs, out _);
        }
    }
}

done:
Console.WriteLine();
Console.WriteLine($"[Client] Sent {MessageCount} messages, received {echosReceived} echoes.");
Console.WriteLine("[Client] Disconnecting...");

server.DisconnectLater(0);

// Drain until disconnected or timeout
var disconnectDeadline = Environment.TickCount64 + 3_000;
while (Environment.TickCount64 < disconnectDeadline)
{
    if (host.Service(ServiceTimeoutMs, out Event evt) > 0)
    {
        if (evt.Type is EventType.Disconnect or EventType.Timeout)
        {
            Console.WriteLine("[Client] Disconnected cleanly.");
            break;
        }
    }
}

host.Flush();
Library.Deinitialize();
Console.WriteLine("[Client] Done.");
