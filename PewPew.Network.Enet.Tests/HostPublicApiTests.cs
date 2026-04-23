using System;
using System.Net;
using Xunit;
using PewPew.Network.Enet.Internal;
using PewPew.Network.Enet.Tests.Fakes;

namespace PewPew.Network.Enet.Tests
{
    public class HostPublicApiTests
    {
        private static readonly IPEndPoint ServerEndPoint =
            new IPEndPoint(IPAddress.Loopback.MapToIPv6(), 7790);
        private static readonly IPEndPoint ClientEndPoint =
            new IPEndPoint(IPAddress.Loopback.MapToIPv6(), 11020);

        private static (ENetHostInternal server, ENetHostInternal client, Address serverAddr)
            CreatePair()
        {
            var (clientSocket, serverSocket) = FakeUdpSocket.CreatePair(ClientEndPoint, ServerEndPoint);

            var serverAddr = new Address();
            serverAddr.SetIP("127.0.0.1");
            serverAddr.Port = 7790;

            var server = new ENetHostInternal(serverSocket);
            server.Create(serverAddr, 10, 0, 0, 0, 0);

            var client = new ENetHostInternal(clientSocket);
            client.Create(null, 10, 0, 0, 0, 0);

            return (server, client, serverAddr);
        }

        // ── Host.Create validation ─────────────────────────────────────────────

        [Fact]
        public void Host_Create_WithNegativePeerLimit_Throws()
        {
            var host = new Host();
            Assert.Throws<ArgumentOutOfRangeException>(() => host.Create(null, -1, 0));
        }

        [Fact]
        public void Host_Create_WithPeerLimitExceedingMax_Throws()
        {
            var host = new Host();
            Assert.Throws<ArgumentOutOfRangeException>(() => host.Create(null, Library.MaxPeers + 1, 0));
        }

        [Fact]
        public void Host_Create_WithNegativeChannelLimit_Throws()
        {
            var host = new Host();
            Assert.Throws<ArgumentOutOfRangeException>(() => host.Create(null, 1, -1));
        }

        [Fact]
        public void Host_Create_WithChannelLimitExceedingMax_Throws()
        {
            var host = new Host();
            Assert.Throws<ArgumentOutOfRangeException>(() => host.Create(null, 1, Library.MaxChannelCount + 1));
        }

        [Fact]
        public void Host_Create_CalledTwice_Throws()
        {
            var host = new Host();
            host.Create(null, 1, 0);
            Assert.Throws<InvalidOperationException>(() => host.Create(null, 1, 0));
            host.Dispose();
        }

        // ── Host.IsSet ────────────────────────────────────────────────────────

        [Fact]
        public void Host_IsSet_BeforeCreate_IsFalse()
        {
            var host = new Host();
            Assert.False(host.IsSet);
        }

        [Fact]
        public void Host_IsSet_AfterCreate_IsTrue()
        {
            var host = new Host();
            host.Create(null, 1, 0);
            Assert.True(host.IsSet);
            host.Dispose();
        }

        [Fact]
        public void Host_IsSet_AfterDispose_IsFalse()
        {
            var host = new Host();
            host.Create(null, 1, 0);
            host.Dispose();
            Assert.False(host.IsSet);
        }

        // ── Host ThrowIfNotCreated on properties/methods ──────────────────────

        [Fact]
        public void Host_PeersCount_BeforeCreate_Throws()
        {
            var host = new Host();
            Assert.Throws<InvalidOperationException>(() => _ = host.PeersCount);
        }

        [Fact]
        public void Host_PacketsSent_BeforeCreate_Throws()
        {
            var host = new Host();
            Assert.Throws<InvalidOperationException>(() => _ = host.PacketsSent);
        }

        [Fact]
        public void Host_PacketsReceived_BeforeCreate_Throws()
        {
            var host = new Host();
            Assert.Throws<InvalidOperationException>(() => _ = host.PacketsReceived);
        }

        [Fact]
        public void Host_BytesSent_BeforeCreate_Throws()
        {
            var host = new Host();
            Assert.Throws<InvalidOperationException>(() => _ = host.BytesSent);
        }

        [Fact]
        public void Host_BytesReceived_BeforeCreate_Throws()
        {
            var host = new Host();
            Assert.Throws<InvalidOperationException>(() => _ = host.BytesReceived);
        }

        [Fact]
        public void Host_Flush_BeforeCreate_Throws()
        {
            var host = new Host();
            Assert.Throws<InvalidOperationException>(() => host.Flush());
        }

        [Fact]
        public void Host_SetChannelLimit_BeforeCreate_Throws()
        {
            var host = new Host();
            Assert.Throws<InvalidOperationException>(() => host.SetChannelLimit(2));
        }

        [Fact]
        public void Host_SetBandwidthLimit_BeforeCreate_Throws()
        {
            var host = new Host();
            Assert.Throws<InvalidOperationException>(() => host.SetBandwidthLimit(0, 0));
        }

        // ── Host.Connect validation ───────────────────────────────────────────

        [Fact]
        public void Host_Connect_WithNegativeChannelLimit_Throws()
        {
            var host = new Host();
            host.Create(null, 1, 0);
            var addr = new Address();
            addr.SetIP("127.0.0.1");
            addr.Port = 7000;
            Assert.Throws<ArgumentOutOfRangeException>(() => host.Connect(addr, -1, 0));
            host.Dispose();
        }

        [Fact]
        public void Host_Connect_WithChannelLimitExceedingMax_Throws()
        {
            var host = new Host();
            host.Create(null, 1, 0);
            var addr = new Address();
            addr.SetIP("127.0.0.1");
            addr.Port = 7000;
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                host.Connect(addr, Library.MaxChannelCount + 1, 0));
            host.Dispose();
        }

        // ── Host.Service validation ───────────────────────────────────────────

        [Fact]
        public void Host_Service_WithNegativeTimeout_Throws()
        {
            var host = new Host();
            host.Create(null, 1, 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => host.Service(-1, out _));
            host.Dispose();
        }

        // ── Host.SetChannelLimit validation ───────────────────────────────────

        [Fact]
        public void Host_SetChannelLimit_WithNegativeValue_Throws()
        {
            var host = new Host();
            host.Create(null, 1, 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => host.SetChannelLimit(-1));
            host.Dispose();
        }

        [Fact]
        public void Host_SetChannelLimit_WithValueExceedingMax_Throws()
        {
            var host = new Host();
            host.Create(null, 1, 0);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => host.SetChannelLimit(Library.MaxChannelCount + 1));
            host.Dispose();
        }

        // ── Stats start at zero ────────────────────────────────────────────────

        [Fact]
        public void Host_Stats_AfterCreate_AreZero()
        {
            var (_, client, _) = CreatePair();

            Assert.Equal(0u, client.PacketsSent);
            Assert.Equal(0u, client.PacketsReceived);
            Assert.Equal(0u, (uint)client.TotalSentData);
            Assert.Equal(0u, client.TotalReceivedData);
        }

        // ── Host.Dispose idempotency ──────────────────────────────────────────

        [Fact]
        public void Host_Dispose_CalledTwice_DoesNotThrow()
        {
            var host = new Host();
            host.Create(null, 1, 0);
            host.Dispose();
            var ex = Record.Exception(() => host.Dispose());
            Assert.Null(ex);
        }

        [Fact]
        public void Host_Dispose_WithoutCreate_DoesNotThrow()
        {
            var host = new Host();
            var ex = Record.Exception(() => host.Dispose());
            Assert.Null(ex);
        }
    }
}
