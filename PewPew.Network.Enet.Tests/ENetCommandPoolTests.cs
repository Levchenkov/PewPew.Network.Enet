using Xunit;
using PewPew.Network.Enet.Internal;

namespace PewPew.Network.Enet.Tests
{
    public class ENetCommandPoolTests
    {
        // ── Outgoing ──────────────────────────────────────────────────────────

        [Fact]
        public void GetOutgoing_ReturnsNonNull()
        {
            var pool = new ENetCommandPool();
            var cmd = pool.GetOutgoing();
            Assert.NotNull(cmd);
        }

        [Fact]
        public void GetOutgoing_ReturnsFreshInstance_WithZeroedFields()
        {
            var pool = new ENetCommandPool();
            var cmd = pool.GetOutgoing();

            Assert.Equal(0, cmd.ReliableSequenceNumber);
            Assert.Equal(0, cmd.UnreliableSequenceNumber);
            Assert.Equal(0u, cmd.SentTime);
            Assert.Equal(0u, cmd.RoundTripTimeout);
            Assert.Equal(0u, cmd.RoundTripTimeoutLimit);
            Assert.Equal(0u, cmd.FragmentOffset);
            Assert.Equal(0, cmd.FragmentLength);
            Assert.Equal(0, cmd.SendAttempts);
            Assert.Null(cmd.Packet);
        }

        [Fact]
        public void ReturnOutgoing_ThenGet_ReusesObject()
        {
            var pool = new ENetCommandPool();
            var cmd1 = pool.GetOutgoing();
            pool.ReturnOutgoing(cmd1);
            var cmd2 = pool.GetOutgoing();

            Assert.Same(cmd1, cmd2);
        }

        [Fact]
        public void ReturnOutgoing_ResetsAllFields()
        {
            var pool = new ENetCommandPool();
            var cmd = pool.GetOutgoing();

            // Dirty all fields
            cmd.ReliableSequenceNumber = 0xFF;
            cmd.UnreliableSequenceNumber = 0x10;
            cmd.SentTime = 999;
            cmd.RoundTripTimeout = 123;
            cmd.RoundTripTimeoutLimit = 456;
            cmd.FragmentOffset = 1024;
            cmd.FragmentLength = 500;
            cmd.SendAttempts = 3;
            cmd.Packet = ENetPacket.Create(new byte[] { 1 }, 1, ENetPacketFlag.None);

            pool.ReturnOutgoing(cmd);
            var reused = pool.GetOutgoing();

            Assert.Equal(0, reused.ReliableSequenceNumber);
            Assert.Equal(0, reused.UnreliableSequenceNumber);
            Assert.Equal(0u, reused.SentTime);
            Assert.Equal(0u, reused.RoundTripTimeout);
            Assert.Equal(0u, reused.RoundTripTimeoutLimit);
            Assert.Equal(0u, reused.FragmentOffset);
            Assert.Equal(0, reused.FragmentLength);
            Assert.Equal(0, reused.SendAttempts);
            Assert.Null(reused.Packet);
        }

        [Fact]
        public void ReturnOutgoing_ExceedingMaxPoolSize_DoesNotCrash()
        {
            var pool = new ENetCommandPool();
            // MaxPoolSize is 512 — return 520 items
            for (int i = 0; i < 520; i++)
                pool.ReturnOutgoing(new ENetOutgoingCommand());

            // Pool is capped; getting one item should still work
            var cmd = pool.GetOutgoing();
            Assert.NotNull(cmd);
        }

        // ── Acknowledgement ───────────────────────────────────────────────────

        [Fact]
        public void GetAck_ReturnsNonNull()
        {
            var pool = new ENetCommandPool();
            Assert.NotNull(pool.GetAck());
        }

        [Fact]
        public void ReturnAck_ThenGet_ReusesObject()
        {
            var pool = new ENetCommandPool();
            var ack1 = pool.GetAck();
            pool.ReturnAck(ack1);
            var ack2 = pool.GetAck();
            Assert.Same(ack1, ack2);
        }

        [Fact]
        public void ReturnAck_ResetsFields()
        {
            var pool = new ENetCommandPool();
            var ack = pool.GetAck();
            ack.SentTime = 1234;

            pool.ReturnAck(ack);
            var reused = pool.GetAck();

            Assert.Equal(0u, reused.SentTime);
        }

        [Fact]
        public void ReturnAck_ExceedingMaxPoolSize_DoesNotCrash()
        {
            var pool = new ENetCommandPool();
            for (int i = 0; i < 520; i++)
                pool.ReturnAck(new ENetAcknowledgement());
            Assert.NotNull(pool.GetAck());
        }

        // ── Incoming ──────────────────────────────────────────────────────────

        [Fact]
        public void GetIncoming_ReturnsNonNull()
        {
            var pool = new ENetCommandPool();
            Assert.NotNull(pool.GetIncoming());
        }

        [Fact]
        public void ReturnIncoming_ThenGet_ReusesObject()
        {
            var pool = new ENetCommandPool();
            var inc1 = pool.GetIncoming();
            pool.ReturnIncoming(inc1);
            var inc2 = pool.GetIncoming();
            Assert.Same(inc1, inc2);
        }

        [Fact]
        public void ReturnIncoming_ResetsAllFields()
        {
            var pool = new ENetCommandPool();
            var inc = pool.GetIncoming();
            inc.ReliableSequenceNumber = 0xFF;
            inc.UnreliableSequenceNumber = 0x10;
            inc.FragmentCount = 5;
            inc.FragmentsRemaining = 3;
            inc.Fragments = new uint[] { 1, 2 };
            inc.Packet = ENetPacket.Create(new byte[] { 1 }, 1, ENetPacketFlag.None);

            pool.ReturnIncoming(inc);
            var reused = pool.GetIncoming();

            Assert.Equal(0, reused.ReliableSequenceNumber);
            Assert.Equal(0, reused.UnreliableSequenceNumber);
            Assert.Equal(0u, reused.FragmentCount);
            Assert.Equal(0u, reused.FragmentsRemaining);
            Assert.Null(reused.Fragments);
            Assert.Null(reused.Packet);
        }

        [Fact]
        public void ReturnIncoming_ExceedingMaxPoolSize_DoesNotCrash()
        {
            var pool = new ENetCommandPool();
            for (int i = 0; i < 520; i++)
                pool.ReturnIncoming(new ENetIncomingCommand());
            Assert.NotNull(pool.GetIncoming());
        }
    }
}
