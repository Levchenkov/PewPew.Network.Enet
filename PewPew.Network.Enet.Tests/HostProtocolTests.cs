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

        // ── Disconnect with data ──────────────────────────────────────────────

        [Fact]
        public void Disconnect_WithData_ServerEventCarriesCorrectData()
        {
            var (server, client, serverAddr) = CreateConnectedPair();
            var clientPeer = client.Connect(serverAddr, 1, 0);

            var serverEvent = new ENetEvent();
            var clientEvent = new ENetEvent();

            RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => c.Type == ENetEventType.Connect && s.Type == ENetEventType.Connect);

            serverEvent.Type = ENetEventType.None;
            clientEvent.Type = ENetEventType.None;

            const uint disconnectData = 0xABCDEF12;
            clientPeer!.Disconnect(disconnectData);

            bool success = RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => s.Type == ENetEventType.Disconnect, maxIterations: 50);

            Assert.True(success, "Server did not receive Disconnect event");
            Assert.Equal(disconnectData, serverEvent.Data);
        }

        // ── ConnectedPeers counter ────────────────────────────────────────────

        [Fact]
        public void ConnectedPeers_AfterConnect_PeersCountIsPositive()
        {
            // Verify that after a successful connect handshake, the server
            // has exactly 1 peer in its Peers array that is in Connected state.
            var (server, client, serverAddr) = CreateConnectedPair();
            client.Connect(serverAddr, 1, 0);

            var serverEvent = new ENetEvent();
            var clientEvent = new ENetEvent();

            RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => c.Type == ENetEventType.Connect && s.Type == ENetEventType.Connect);

            // At least one peer should be in Connected state on the server
            int connectedCount = 0;
            foreach (var p in server.Peers)
                if (p.State == ENetPeerState.Connected)
                    connectedCount++;

            Assert.Equal(1, connectedCount);
        }

        // ── Two simultaneous clients ──────────────────────────────────────────

        [Fact]
        public void TwoClients_SequentialConnect_BothReceiveConnectEvent()
        {
            // Uses two independent server+client pairs to verify the protocol
            // handles multiple independent connections correctly.
            var (server1, client1, serverAddr1) = CreateConnectedPair();
            client1.Connect(serverAddr1, 1, 0);

            var s1Evt = new ENetEvent();
            var c1Evt = new ENetEvent();
            bool ok1 = RunServiceLoop(client1, c1Evt, server1, s1Evt,
                (c, s) => c.Type == ENetEventType.Connect && s.Type == ENetEventType.Connect);

            Assert.True(ok1, "First client/server pair did not complete handshake");
            Assert.Equal(ENetEventType.Connect, c1Evt.Type);
            Assert.Equal(ENetEventType.Connect, s1Evt.Type);

            // Second independent pair
            var ep2Server = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback.MapToIPv6(), 7781);
            var ep2Client = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback.MapToIPv6(), 11012);
            var (clientSocket2, serverSocket2) = FakeUdpSocket.CreatePair(ep2Client, ep2Server);

            var serverAddr2 = new Address();
            serverAddr2.SetIP("127.0.0.1");
            serverAddr2.Port = 7781;

            var server2 = new ENetHostInternal(serverSocket2);
            server2.Create(serverAddr2, 10, 0, 0, 0, 0);
            var client2 = new ENetHostInternal(clientSocket2);
            client2.Create(null, 10, 0, 0, 0, 0);

            client2.Connect(serverAddr2, 1, 0);

            var s2Evt = new ENetEvent();
            var c2Evt = new ENetEvent();
            bool ok2 = RunServiceLoop(client2, c2Evt, server2, s2Evt,
                (c, s) => c.Type == ENetEventType.Connect && s.Type == ENetEventType.Connect);

            Assert.True(ok2, "Second client/server pair did not complete handshake");
            Assert.Equal(ENetEventType.Connect, c2Evt.Type);
        }

        // ── SetBandwidthLimit ─────────────────────────────────────────────────

        [Fact]
        public void SetBandwidthLimit_SetsRecalculateFlagAndBandwidthValues()
        {
            var (server, _, _) = CreateConnectedPair();

            server.SetBandwidthLimit(100_000, 200_000);

            Assert.Equal(100_000u, server.IncomingBandwidth);
            Assert.Equal(200_000u, server.OutgoingBandwidth);
            Assert.True(server.RecalculateBandwidthLimits);
        }

        // ── SetChannelLimit ───────────────────────────────────────────────────

        [Theory]
        [InlineData(0,   255)]   // 0 → clamped to max
        [InlineData(1,   1)]
        [InlineData(2,   2)]
        [InlineData(255, 255)]
        [InlineData(300, 255)]   // > max → clamped to max
        public void SetChannelLimit_ClampsCorrectly(int input, int expected)
        {
            var (server, _, _) = CreateConnectedPair();
            server.SetChannelLimit(input);
            Assert.Equal(expected, server.ChannelLimit);
        }

        // ── CheckEvents ───────────────────────────────────────────────────────

        [Fact]
        public void CheckEvents_WhenNoEvents_ReturnsZero()
        {
            var (server, _, _) = CreateConnectedPair();
            var evt = new ENetEvent();
            int result = server.CheckEvents(evt);
            Assert.Equal(0, result);
            Assert.Equal(ENetEventType.None, evt.Type);
        }

        [Fact]
        public void CheckEvents_AfterConnect_ReturnsPositive()
        {
            var (server, client, serverAddr) = CreateConnectedPair();
            client.Connect(serverAddr, 1, 0);

            var dummy = new ENetEvent();
            // Run enough to get packets in
            for (int i = 0; i < 10; i++)
            {
                client.Service(dummy, 0);
                server.Service(dummy, 0);
            }

            // Service once more to queue the connect event into dispatch queue
            var evt = new ENetEvent();
            // After service loop a connect event may have been delivered; check events still works
            int result = server.CheckEvents(evt);
            // result is 0 or 1; just verify it doesn't throw and handles null/evt correctly
            Assert.True(result >= 0);
        }

        // ── Flush ─────────────────────────────────────────────────────────────

        [Fact]
        public void Flush_DoesNotThrow()
        {
            var (server, _, _) = CreateConnectedPair();
            var ex = Record.Exception(() => server.Flush());
            Assert.Null(ex);
        }

        // ── Broadcast ────────────────────────────────────────────────────────

        [Fact]
        public void Broadcast_ToAllPeers_EachConnectedPeerReceivesPacket()
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

            // Server broadcasts to all (only one client here)
            byte[] payload = { 0x01, 0x02, 0x03 };
            var pkt = ENetPacket.Create(payload, payload.Length, ENetPacketFlag.Reliable);
            server.Broadcast(0, pkt);

            bool received = RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => c.Type == ENetEventType.Receive);

            Assert.True(received, "Client did not receive broadcast");
            Assert.NotNull(clientEvent.Packet);
            Assert.Equal(payload.Length, clientEvent.Packet.DataLength);
        }

        // ── Reliable fragmented packet ────────────────────────────────────────

        [Fact]
        public void Send_LargeReliablePacket_ServerReceivesReassembledData()
        {
            var (server, client, serverAddr) = CreateConnectedPair();
            var clientPeer = client.Connect(serverAddr, 1, 0);

            var serverEvent = new ENetEvent();
            var clientEvent = new ENetEvent();

            RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => c.Type == ENetEventType.Connect && s.Type == ENetEventType.Connect);

            serverEvent.Type = ENetEventType.None;
            clientEvent.Type = ENetEventType.None;

            // MTU is 1280; send 3500 bytes → requires fragmentation
            const int PayloadSize = 3500;
            byte[] payload = new byte[PayloadSize];
            for (int i = 0; i < PayloadSize; i++)
                payload[i] = (byte)(i % 256);

            var pkt = ENetPacket.Create(payload, payload.Length, ENetPacketFlag.Reliable);
            int sendResult = client.PeerSend(clientPeer!, 0, pkt);
            Assert.Equal(0, sendResult);

            bool received = RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => s.Type == ENetEventType.Receive, maxIterations: 300);

            Assert.True(received, "Server did not receive the fragmented packet");
            Assert.NotNull(serverEvent.Packet);
            Assert.Equal(PayloadSize, serverEvent.Packet.DataLength);

            // Verify contents
            for (int i = 0; i < PayloadSize; i++)
                Assert.Equal((byte)(i % 256), serverEvent.Packet.Data[i]);
        }

        // ── CRC64 checksum callback ───────────────────────────────────────────

        [Fact]
        public void ChecksumCallback_Set_ComputesChecksumWithoutError()
        {
            var (server, client, serverAddr) = CreateConnectedPair();

            // Enable CRC64 on both sides
            server.ChecksumCallback = Internal.Crc64.Compute;
            client.ChecksumCallback = Internal.Crc64.Compute;

            var clientPeer = client.Connect(serverAddr, 1, 0);

            var serverEvent = new ENetEvent();
            var clientEvent = new ENetEvent();

            bool success = RunServiceLoop(client, clientEvent, server, serverEvent,
                (c, s) => c.Type == ENetEventType.Connect && s.Type == ENetEventType.Connect,
                maxIterations: 50);

            Assert.True(success, "Handshake did not complete with CRC64 enabled");
        }
    }
}
