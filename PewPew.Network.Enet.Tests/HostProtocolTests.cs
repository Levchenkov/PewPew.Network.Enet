using System;
using System.Text;
using System.Net;
using Xunit;
using PewPew.Network.Enet.Internal;
using PewPew.Network.Enet.Tests.Fakes;

namespace PewPew.Network.Enet.Tests
{
    /// <summary>
    /// Protocol-level tests using FakeUdpSocket. No real sockets are opened.
    /// </summary>
    public class HostProtocolTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private static readonly IPEndPoint ServerEndPoint =
            new IPEndPoint(IPAddress.Loopback.MapToIPv6(), 7777);
        private static readonly IPEndPoint ClientEndPoint =
            new IPEndPoint(IPAddress.Loopback.MapToIPv6(), 11001);

        private static (ENetHostInternal server, ENetHostInternal client, Address serverAddr)
            CreateConnectedPair()
        {
            var (clientSocket, serverSocket) = FakeUdpSocket.CreatePair(ClientEndPoint, ServerEndPoint);

            var serverAddr = new Address();
            serverAddr.SetIP("127.0.0.1");
            serverAddr.Port = 7777;

            var server = new ENetHostInternal(serverSocket);
            server.Create(serverAddr, 10, 0, 0, 0, 0);

            var client = new ENetHostInternal(clientSocket);
            client.Create(null, 10, 0, 0, 0, 0);

            return (server, client, serverAddr);
        }

        /// <summary>
        /// Run both sides in a tight loop until a predicate is satisfied or iterations run out.
        /// Uses accumulated events so conditions requiring events from different iterations
        /// (e.g., client connect + server connect) are handled correctly.
        /// </summary>
        private static bool RunServiceLoop(
            ENetHostInternal client, ENetEvent clientEvent,
            ENetHostInternal server, ENetEvent serverEvent,
            System.Func<ENetEvent, ENetEvent, bool> stopCondition,
            int maxIterations = 30)
        {
            // Accumulated "sticky" events — once an event fires it stays visible to the
            // stop condition even if the next Service() call resets the local event.
            var clientAcc = new ENetEvent();
            var serverAcc = new ENetEvent();

            for (int i = 0; i < maxIterations; i++)
            {
                client.Service(clientEvent, 0);
                server.Service(serverEvent, 0);

                if (clientEvent.Type != ENetEventType.None)
                {
                    clientAcc.Type = clientEvent.Type;
                    clientAcc.Peer = clientEvent.Peer;
                    clientAcc.Packet = clientEvent.Packet;
                    clientAcc.Data = clientEvent.Data;
                }
                if (serverEvent.Type != ENetEventType.None)
                {
                    serverAcc.Type = serverEvent.Type;
                    serverAcc.Peer = serverEvent.Peer;
                    serverAcc.Packet = serverEvent.Packet;
                    serverAcc.Data = serverEvent.Data;
                }

                if (stopCondition(clientAcc, serverAcc))
                {
                    // Copy accumulated state back so callers see the final event values.
                    clientEvent.Type = clientAcc.Type; clientEvent.Peer = clientAcc.Peer;
                    clientEvent.Packet = clientAcc.Packet; clientEvent.Data = clientAcc.Data;
                    serverEvent.Type = serverAcc.Type; serverEvent.Peer = serverAcc.Peer;
                    serverEvent.Packet = serverAcc.Packet; serverEvent.Data = serverAcc.Data;
                    return true;
                }
            }

            return false;
        }

        // ── Connect handshake ─────────────────────────────────────────────────

        [Fact]
        public void Connect_ClientConnectsToServer_ServerReceivesConnectEvent()
        {
            var (server, client, serverAddr) = CreateConnectedPair();

            client.Connect(serverAddr, 1, 0);

            var serverEvent = new ENetEvent();
            var clientEvent = new ENetEvent();

            bool success = RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => s.Type == ENetEventType.Connect);

            Assert.True(success, "Server did not receive Connect event within the allowed iterations");
            Assert.Equal(ENetEventType.Connect, serverEvent.Type);
            Assert.NotNull(serverEvent.Peer);
        }

        [Fact]
        public void Connect_ClientConnectsToServer_ClientReceivesConnectEvent()
        {
            var (server, client, serverAddr) = CreateConnectedPair();

            client.Connect(serverAddr, 1, 0);

            var serverEvent = new ENetEvent();
            var clientEvent = new ENetEvent();

            bool success = RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => c.Type == ENetEventType.Connect);

            Assert.True(success, "Client did not receive Connect event within the allowed iterations");
            Assert.Equal(ENetEventType.Connect, clientEvent.Type);
        }

        [Fact]
        public void Connect_AfterHandshake_PeerStateIsConnected()
        {
            var (server, client, serverAddr) = CreateConnectedPair();

            var clientPeer = client.Connect(serverAddr, 1, 0);
            Assert.NotNull(clientPeer);

            var serverEvent = new ENetEvent();
            var clientEvent = new ENetEvent();

            // Wait for client connect event
            RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => c.Type == ENetEventType.Connect);

            Assert.Equal(ENetPeerState.Connected, clientPeer.State);
        }

        // ── Reliable send / receive ───────────────────────────────────────────

        [Fact]
        public void Send_ReliablePacket_ServerReceivesCorrectData()
        {
            var (server, client, serverAddr) = CreateConnectedPair();

            var clientPeer = client.Connect(serverAddr, 1, 0);
            var serverEvent = new ENetEvent();
            var clientEvent = new ENetEvent();

            // Complete handshake
            RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => c.Type == ENetEventType.Connect && s.Type == ENetEventType.Connect);

            // Reset events
            serverEvent.Type = ENetEventType.None;
            clientEvent.Type = ENetEventType.None;

            // Send a reliable packet from client
            byte[] payload = { 0xDE, 0xAD, 0xBE, 0xEF };
            var pkt = ENetPacket.Create(payload, payload.Length, ENetPacketFlag.Reliable);
            client.PeerSend(clientPeer!, 0, pkt);

            // Service until server receives
            bool received = RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => s.Type == ENetEventType.Receive);

            Assert.True(received, "Server did not receive the packet");
            Assert.Equal(ENetEventType.Receive, serverEvent.Type);
            Assert.NotNull(serverEvent.Packet);
            Assert.Equal(payload.Length, serverEvent.Packet.DataLength);
            Assert.Equal(payload[0], serverEvent.Packet.Data[0]);
            Assert.Equal(payload[3], serverEvent.Packet.Data[3]);
        }

        [Fact]
        public void Send_UnreliablePacket_ServerReceivesCorrectData()
        {
            var (server, client, serverAddr) = CreateConnectedPair();

            var clientPeer = client.Connect(serverAddr, 1, 0);
            var serverEvent = new ENetEvent();
            var clientEvent = new ENetEvent();

            RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => c.Type == ENetEventType.Connect && s.Type == ENetEventType.Connect);

            serverEvent.Type = ENetEventType.None;
            clientEvent.Type = ENetEventType.None;

            byte[] payload = { 1, 2, 3 };
            var pkt = ENetPacket.Create(payload, payload.Length, ENetPacketFlag.None);
            client.PeerSend(clientPeer!, 0, pkt);

            bool received = RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => s.Type == ENetEventType.Receive);

            Assert.True(received, "Server did not receive the unreliable packet");
            Assert.Equal(payload.Length, serverEvent.Packet!.DataLength);
        }

        // ── Disconnect ────────────────────────────────────────────────────────

        [Fact]
        public void Disconnect_ClientDisconnects_ServerReceivesDisconnectEvent()
        {
            var (server, client, serverAddr) = CreateConnectedPair();

            var clientPeer = client.Connect(serverAddr, 1, 0);
            var serverEvent = new ENetEvent();
            var clientEvent = new ENetEvent();

            // Complete handshake
            RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => c.Type == ENetEventType.Connect && s.Type == ENetEventType.Connect);

            serverEvent.Type = ENetEventType.None;
            clientEvent.Type = ENetEventType.None;

            // Initiate graceful disconnect
            clientPeer!.Disconnect(42);

            bool success = RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => s.Type == ENetEventType.Disconnect, maxIterations: 50);

            Assert.True(success, "Server did not receive Disconnect event");
            Assert.Equal(ENetEventType.Disconnect, serverEvent.Type);
        }

        // ── PreventConnections ────────────────────────────────────────────────

        [Fact]
        public void PreventConnections_True_ClientDoesNotReceiveConnectEvent()
        {
            var (server, client, serverAddr) = CreateConnectedPair();
            server.PreventConnections = true;

            client.Connect(serverAddr, 1, 0);

            var serverEvent = new ENetEvent();
            var clientEvent = new ENetEvent();

            bool clientConnected = RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => c.Type == ENetEventType.Connect, maxIterations: 20);

            Assert.False(clientConnected, "Client should NOT have connected when PreventConnections is true");
        }

        // ── Statistics ────────────────────────────────────────────────────────

        [Fact]
        public void Service_AfterSendingPackets_PacketsSentCounterIncreases()
        {
            var (server, client, serverAddr) = CreateConnectedPair();

            var clientPeer = client.Connect(serverAddr, 1, 0);
            var serverEvent = new ENetEvent();
            var clientEvent = new ENetEvent();

            RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => c.Type == ENetEventType.Connect && s.Type == ENetEventType.Connect);

            uint packetsBefore = client.PacketsSent;

            serverEvent.Type = ENetEventType.None;
            clientEvent.Type = ENetEventType.None;

            var pkt = ENetPacket.Create(new byte[] { 1 }, 1, ENetPacketFlag.None);
            client.PeerSend(clientPeer!, 0, pkt);

            RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => s.Type == ENetEventType.Receive);

            Assert.True(client.PacketsSent > packetsBefore);
        }

        // ── Echo server ───────────────────────────────────────────────────────

        /// <summary>
        /// Mirrors the Demo.Server / Demo.Client flow:
        ///   client connects → sends N "Hello #i" messages →
        ///   server echoes each back → client verifies every echo →
        ///   client disconnects → server receives Disconnect event.
        /// </summary>
        [Fact]
        public void EchoServer_ClientSendsMessages_ReceivesAllEchoes()
        {
            const int MessageCount = 5;

            var (server, client, serverAddr) = CreateConnectedPair();
            var clientPeer = client.Connect(serverAddr, 2, 0);

            var serverEvent = new ENetEvent();
            var clientEvent = new ENetEvent();

            // ── Handshake ──────────────────────────────────────────────────────
            bool connected = RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => c.Type == ENetEventType.Connect && s.Type == ENetEventType.Connect);

            Assert.True(connected, "Handshake did not complete");
            var serverPeerForClient = serverEvent.Peer!;

            // ── Send / echo loop ───────────────────────────────────────────────
            int echosReceived = 0;

            for (int i = 1; i <= MessageCount; i++)
            {
                serverEvent.Type = ENetEventType.None;
                clientEvent.Type = ENetEventType.None;

                // Client sends "Hello #i" reliably on channel 0
                string text = $"Hello #{i}";
                byte[] data = Encoding.UTF8.GetBytes(text);
                var pkt = ENetPacket.Create(data, data.Length, ENetPacketFlag.Reliable);
                client.PeerSend(clientPeer!, 0, pkt);

                // Wait for server to receive the packet
                bool serverReceived = RunServiceLoop(client, clientEvent, server, serverEvent,
                    (c, s) => s.Type == ENetEventType.Receive);

                Assert.True(serverReceived, $"Server did not receive message #{i}");
                Assert.NotNull(serverEvent.Packet);

                // Server echoes back the exact bytes
                byte[] echoData = new byte[serverEvent.Packet.DataLength];
                Array.Copy(serverEvent.Packet.Data, echoData, echoData.Length);
                var echoPkt = ENetPacket.Create(echoData, echoData.Length, ENetPacketFlag.Reliable);
                server.PeerSend(serverPeerForClient, 1, echoPkt);

                // Reset and wait for client to receive the echo
                serverEvent.Type = ENetEventType.None;
                clientEvent.Type = ENetEventType.None;

                bool clientReceived = RunServiceLoop(client, clientEvent, server, serverEvent,
                    (c, s) => c.Type == ENetEventType.Receive);

                Assert.True(clientReceived, $"Client did not receive echo #{i}");
                Assert.NotNull(clientEvent.Packet);

                string echo = Encoding.UTF8.GetString(
                    clientEvent.Packet.Data, 0, clientEvent.Packet.DataLength);
                Assert.Equal(text, echo);

                echosReceived++;
            }

            Assert.Equal(MessageCount, echosReceived);

            // ── Disconnect ─────────────────────────────────────────────────────
            serverEvent.Type = ENetEventType.None;
            clientEvent.Type = ENetEventType.None;

            clientPeer!.Disconnect(0);

            bool disconnected = RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => s.Type == ENetEventType.Disconnect, maxIterations: 60);

            Assert.True(disconnected, "Server did not receive Disconnect event after echo exchange");
            Assert.Equal(ENetEventType.Disconnect, serverEvent.Type);
        }
    }
}
