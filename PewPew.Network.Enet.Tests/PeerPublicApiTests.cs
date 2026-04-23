using System;
using System.Net;
using Xunit;
using PewPew.Network.Enet.Internal;
using PewPew.Network.Enet.Tests.Fakes;

namespace PewPew.Network.Enet.Tests
{
    public class PeerPublicApiTests
    {
        private static readonly IPEndPoint ServerEp =
            new IPEndPoint(IPAddress.Loopback.MapToIPv6(), 7795);
        private static readonly IPEndPoint ClientEp =
            new IPEndPoint(IPAddress.Loopback.MapToIPv6(), 11025);

        private static (ENetHostInternal server, ENetHostInternal client, Address serverAddr)
            CreatePair()
        {
            var (clientSocket, serverSocket) = FakeUdpSocket.CreatePair(ClientEp, ServerEp);
            var serverAddr = new Address();
            serverAddr.SetIP("127.0.0.1");
            serverAddr.Port = 7795;

            var server = new ENetHostInternal(serverSocket);
            server.Create(serverAddr, 10, 0, 0, 0, 0);
            var client = new ENetHostInternal(clientSocket);
            client.Create(null, 10, 0, 0, 0, 0);
            return (server, client, serverAddr);
        }

        private static bool RunUntil(
            ENetHostInternal client, ENetEvent clientEvt,
            ENetHostInternal server, ENetEvent serverEvt,
            System.Func<ENetEvent, ENetEvent, bool> cond,
            int maxIter = 40)
        {
            var ca = new ENetEvent();
            var sa = new ENetEvent();
            for (int i = 0; i < maxIter; i++)
            {
                client.Service(clientEvt, 0);
                server.Service(serverEvt, 0);
                if (clientEvt.Type != ENetEventType.None) { ca.Type = clientEvt.Type; ca.Peer = clientEvt.Peer; }
                if (serverEvt.Type != ENetEventType.None) { sa.Type = serverEvt.Type; sa.Peer = serverEvt.Peer; }
                if (cond(ca, sa)) { clientEvt.Type = ca.Type; serverEvt.Type = sa.Type; clientEvt.Peer = ca.Peer; serverEvt.Peer = sa.Peer; return true; }
            }
            return false;
        }

        // ── Peer.IsSet ────────────────────────────────────────────────────────

        [Fact]
        public void Peer_IsSet_ForDefault_IsFalse()
        {
            Peer p = default;
            Assert.False(p.IsSet);
        }

        // ── Properties throw when not set ─────────────────────────────────────

        [Fact]
        public void Peer_ID_WhenNotSet_Throws()
        {
            Peer p = default;
            Assert.Throws<InvalidOperationException>(() => _ = p.ID);
        }

        [Fact]
        public void Peer_IP_WhenNotSet_Throws()
        {
            Peer p = default;
            Assert.Throws<InvalidOperationException>(() => _ = p.IP);
        }

        [Fact]
        public void Peer_Port_WhenNotSet_Throws()
        {
            Peer p = default;
            Assert.Throws<InvalidOperationException>(() => _ = p.Port);
        }

        [Fact]
        public void Peer_MTU_WhenNotSet_Throws()
        {
            Peer p = default;
            Assert.Throws<InvalidOperationException>(() => _ = p.MTU);
        }

        [Fact]
        public void Peer_RoundTripTime_WhenNotSet_Throws()
        {
            Peer p = default;
            Assert.Throws<InvalidOperationException>(() => _ = p.RoundTripTime);
        }

        [Fact]
        public void Peer_PacketsSent_WhenNotSet_Throws()
        {
            Peer p = default;
            Assert.Throws<InvalidOperationException>(() => _ = p.PacketsSent);
        }

        [Fact]
        public void Peer_PacketsLost_WhenNotSet_Throws()
        {
            Peer p = default;
            Assert.Throws<InvalidOperationException>(() => _ = p.PacketsLost);
        }

        [Fact]
        public void Peer_BytesSent_WhenNotSet_Throws()
        {
            Peer p = default;
            Assert.Throws<InvalidOperationException>(() => _ = p.BytesSent);
        }

        [Fact]
        public void Peer_BytesReceived_WhenNotSet_Throws()
        {
            Peer p = default;
            Assert.Throws<InvalidOperationException>(() => _ = p.BytesReceived);
        }

        [Fact]
        public void Peer_Data_Get_WhenNotSet_Throws()
        {
            Peer p = default;
            Assert.Throws<InvalidOperationException>(() => _ = p.Data);
        }

        [Fact]
        public void Peer_Data_Set_WhenNotSet_Throws()
        {
            Peer p = default;
            Assert.Throws<InvalidOperationException>(() => p.Data = "test");
        }

        // ── State returns Uninitialized for default ───────────────────────────

        [Fact]
        public void Peer_State_ForDefault_ReturnsUninitialized()
        {
            Peer p = default;
            Assert.Equal(PeerState.Uninitialized, p.State);
        }

        // ── ConfigureThrottle ──────────────────────────────────────────────────

        [Fact]
        public void Peer_ConfigureThrottle_PropagatesValuesToNativePeer()
        {
            var (server, client, serverAddr) = CreatePair();
            var nativePeer = client.Connect(serverAddr, 1, 0);
            var peer = new Peer(nativePeer!);

            // Complete handshake so peer is in connected state
            var se = new ENetEvent();
            var ce = new ENetEvent();
            RunUntil(client, ce, server, se, (c, s) => c.Type == ENetEventType.Connect);

            peer.ConfigureThrottle(8000, 4, 3, 50);

            Assert.Equal(8000u, nativePeer!.PacketThrottleInterval);
            Assert.Equal(4u, nativePeer.PacketThrottleAcceleration);
            Assert.Equal(3u, nativePeer.PacketThrottleDeceleration);
            Assert.Equal(50u, nativePeer.PacketThrottleThreshold);
        }

        // ── Disconnect / DisconnectNow / DisconnectLater ──────────────────────

        [Fact]
        public void Peer_Disconnect_WhenNotSet_Throws()
        {
            Peer p = default;
            Assert.Throws<InvalidOperationException>(() => p.Disconnect(0));
        }

        [Fact]
        public void Peer_DisconnectNow_WhenNotSet_Throws()
        {
            Peer p = default;
            Assert.Throws<InvalidOperationException>(() => p.DisconnectNow(0));
        }

        [Fact]
        public void Peer_DisconnectLater_WhenNotSet_Throws()
        {
            Peer p = default;
            Assert.Throws<InvalidOperationException>(() => p.DisconnectLater(0));
        }

        [Fact]
        public void Peer_Reset_WhenNotSet_Throws()
        {
            Peer p = default;
            Assert.Throws<InvalidOperationException>(() => p.Reset());
        }

        [Fact]
        public void Peer_Data_CanBeSetAndRetrieved()
        {
            var (server, client, serverAddr) = CreatePair();
            var nativePeer = client.Connect(serverAddr, 1, 0);
            var peer = new Peer(nativePeer!);

            var se = new ENetEvent();
            var ce = new ENetEvent();
            RunUntil(client, ce, server, se, (c, s) => c.Type == ENetEventType.Connect);

            peer.Data = "hello";
            Assert.Equal("hello", peer.Data);
        }
    }
}
