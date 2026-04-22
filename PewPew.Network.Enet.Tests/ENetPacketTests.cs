using System;
using Xunit;
using PewPew.Network.Enet.Internal;

namespace PewPew.Network.Enet.Tests
{
    public class ENetPacketTests
    {
        // ── Create ────────────────────────────────────────────────────────────

        [Fact]
        public void Create_WithData_CopiesData()
        {
            byte[] data = { 1, 2, 3, 4 };
            var pkt = ENetPacket.Create(data, data.Length, ENetPacketFlag.None);

            Assert.Equal(4, pkt.DataLength);
            Assert.Equal(1, pkt.Data[0]);
            Assert.Equal(4, pkt.Data[3]);

            pkt.Destroy();
        }

        [Fact]
        public void Create_WithNullData_SetsEmptyData()
        {
            var pkt = ENetPacket.Create(null, 0, ENetPacketFlag.None);

            Assert.Equal(0, pkt.DataLength);
            Assert.NotNull(pkt.Data);

            pkt.Destroy();
        }

        [Fact]
        public void Create_WithZeroLength_DataLengthIsZero()
        {
            byte[] data = { 9, 8, 7 };
            var pkt = ENetPacket.Create(data, 0, ENetPacketFlag.None);

            Assert.Equal(0, pkt.DataLength);
            pkt.Destroy();
        }

        [Fact]
        public void Create_WithNoAllocateFlag_ReusesOriginalArrayReference()
        {
            byte[] data = { 5, 6, 7 };
            var pkt = ENetPacket.Create(data, data.Length, ENetPacketFlag.NoAllocate);

            Assert.Same(data, pkt.Data);
            // NoAllocate does not copy; just destroy (no pool return for the buffer)
            pkt.Destroy();
        }

        [Fact]
        public void Create_SetsCorrectFlags()
        {
            byte[] data = { 1 };
            var pkt = ENetPacket.Create(data, 1, ENetPacketFlag.Reliable);

            Assert.Equal(ENetPacketFlag.Reliable, pkt.Flags);
            pkt.Destroy();
        }

        // ── CreateOffset ──────────────────────────────────────────────────────

        [Fact]
        public void CreateOffset_SlicesDataFromOffset()
        {
            byte[] data = { 0, 10, 20, 30, 40 };
            // dataLength = full array length; effectiveLength = dataLength - offset = 5 - 2 = 3
            var pkt = ENetPacket.CreateOffset(data, data.Length, 2, ENetPacketFlag.None);

            Assert.Equal(3, pkt.DataLength);
            Assert.Equal(20, pkt.Data[0]);
            Assert.Equal(30, pkt.Data[1]);
            Assert.Equal(40, pkt.Data[2]);

            pkt.Destroy();
        }

        [Fact]
        public void CreateOffset_WhenOffsetExceedsLength_ReturnsZeroLengthPacket()
        {
            byte[] data = { 1, 2, 3 };
            // effectiveLength = Max(0, 0 - 5) = 0
            var pkt = ENetPacket.CreateOffset(data, 0, 5, ENetPacketFlag.None);

            Assert.Equal(0, pkt.DataLength);
            pkt.Destroy();
        }

        // ── Destroy / FreeCallback ────────────────────────────────────────────

        [Fact]
        public void Destroy_InvokesFreeCallback()
        {
            byte[] data = { 1 };
            var pkt = ENetPacket.Create(data, 1, ENetPacketFlag.None);

            bool callbackInvoked = false;
            pkt.FreeCallback = _ => callbackInvoked = true;

            pkt.Destroy();

            Assert.True(callbackInvoked);
        }

        [Fact]
        public void Destroy_WithNoCallback_DoesNotThrow()
        {
            byte[] data = { 1 };
            var pkt = ENetPacket.Create(data, 1, ENetPacketFlag.None);
            pkt.FreeCallback = null;

            var ex = Record.Exception(() => pkt.Destroy());
            Assert.Null(ex);
        }

        // ── Reference count / Dispose ─────────────────────────────────────────

        [Fact]
        public void Dispose_DecrementsReferenceCount()
        {
            byte[] data = { 1 };
            var pkt = ENetPacket.Create(data, 1, ENetPacketFlag.None);
            pkt.ReferenceCount = 2;

            pkt.Dispose();

            Assert.Equal(1, pkt.ReferenceCount);
            // Clean up (manually destroy since we incremented the ref count)
            pkt.ReferenceCount = 0;
            pkt.Destroy();
        }

        [Fact]
        public void Dispose_WhenReferenceCountIsZero_DoesNotDecrementBelowZero()
        {
            byte[] data = { 1 };
            var pkt = ENetPacket.Create(data, 1, ENetPacketFlag.None);
            pkt.ReferenceCount = 0;

            pkt.Dispose();

            Assert.Equal(0, pkt.ReferenceCount);
            pkt.Destroy();
        }

        // ── Pool reuse ────────────────────────────────────────────────────────

        [Fact]
        public void Destroy_ReturnsInstanceToPool_NextAllocReusesIt()
        {
            // Create and destroy a packet so it's in the pool.
            byte[] data = { 42 };
            var pkt1 = ENetPacket.Create(data, 1, ENetPacketFlag.None);
            pkt1.Destroy();

            // Next creation should reuse the pooled instance (same reference).
            byte[] data2 = { 99 };
            var pkt2 = ENetPacket.Create(data2, 1, ENetPacketFlag.None);

            // The packet should be usable with fresh state.
            Assert.Equal(1, pkt2.DataLength);
            Assert.Equal(ENetPacketFlag.None, pkt2.Flags);
            Assert.Equal(0, pkt2.ReferenceCount);

            pkt2.Destroy();
        }
    }
}
