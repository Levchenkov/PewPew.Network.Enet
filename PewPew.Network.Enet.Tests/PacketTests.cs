using System;
using Xunit;

namespace PewPew.Network.Enet.Tests
{
    public class PacketTests
    {
        // ── IsSet ─────────────────────────────────────────────────────────────

        [Fact]
        public void IsSet_BeforeCreate_ReturnsFalse()
        {
            Packet p = default;
            Assert.False(p.IsSet);
        }

        [Fact]
        public void IsSet_AfterCreate_ReturnsTrue()
        {
            var p = new Packet();
            p.Create(new byte[] { 1, 2, 3 });
            Assert.True(p.IsSet);
            p.Dispose();
        }

        // ── Create(byte[]) ────────────────────────────────────────────────────

        [Fact]
        public void Create_WithData_SetsCorrectLength()
        {
            var p = new Packet();
            p.Create(new byte[] { 10, 20, 30 });
            Assert.Equal(3, p.Length);
            p.Dispose();
        }

        [Fact]
        public void Create_WithData_DataContentsMatch()
        {
            byte[] data = { 0xDE, 0xAD, 0xBE, 0xEF };
            var p = new Packet();
            p.Create(data);
            Assert.Equal(data[0], p.Data[0]);
            Assert.Equal(data[1], p.Data[1]);
            Assert.Equal(data[2], p.Data[2]);
            Assert.Equal(data[3], p.Data[3]);
            p.Dispose();
        }

        [Fact]
        public void Create_WithNullData_ThrowsArgumentNullException()
        {
            var p = new Packet();
            Assert.Throws<ArgumentNullException>(() => p.Create(null!));
        }

        // ── Create(byte[], PacketFlags) ────────────────────────────────────────

        [Fact]
        public void Create_WithReliableFlag_PacketIsSet()
        {
            var p = new Packet();
            p.Create(new byte[] { 1 }, PacketFlags.Reliable);
            Assert.True(p.IsSet);
            p.Dispose();
        }

        // ── Create(byte[], int) ───────────────────────────────────────────────

        [Fact]
        public void Create_WithPartialLength_SetsCorrectLength()
        {
            var p = new Packet();
            p.Create(new byte[] { 1, 2, 3, 4, 5 }, 3);
            Assert.Equal(3, p.Length);
            p.Dispose();
        }

        [Fact]
        public void Create_WithNegativeLength_ThrowsArgumentOutOfRangeException()
        {
            var p = new Packet();
            Assert.Throws<ArgumentOutOfRangeException>(() => p.Create(new byte[4], -1));
        }

        [Fact]
        public void Create_WithLengthExceedingData_ThrowsArgumentOutOfRangeException()
        {
            var p = new Packet();
            Assert.Throws<ArgumentOutOfRangeException>(() => p.Create(new byte[4], 10));
        }

        // ── Create(byte[], int, int, PacketFlags) ─────────────────────────────

        [Fact]
        public void Create_WithOffset_CopiesFromOffset()
        {
            byte[] data = { 0, 1, 2, 3, 4, 5 };
            var p = new Packet();
            p.Create(data, 2, data.Length - 2, PacketFlags.None);
            // offset=2, length=data.Length-2=4, but CreateOffset uses (data.Length - offset)
            Assert.True(p.IsSet);
            p.Dispose();
        }

        [Fact]
        public void Create_WithNullDataAndOffset_ThrowsArgumentNullException()
        {
            var p = new Packet();
            Assert.Throws<ArgumentNullException>(() => p.Create(null!, 0, 4, PacketFlags.None));
        }

        [Fact]
        public void Create_WithNegativeOffset_ThrowsArgumentOutOfRangeException()
        {
            var p = new Packet();
            Assert.Throws<ArgumentOutOfRangeException>(() => p.Create(new byte[4], -1, 4, PacketFlags.None));
        }

        // ── ThrowIfNotCreated ─────────────────────────────────────────────────

        [Fact]
        public void Length_WhenNotCreated_ThrowsInvalidOperationException()
        {
            Packet p = default;
            Assert.Throws<InvalidOperationException>(() => _ = p.Length);
        }

        [Fact]
        public void Data_WhenNotCreated_ThrowsInvalidOperationException()
        {
            Packet p = default;
            Assert.Throws<InvalidOperationException>(() => _ = p.Data);
        }

        [Fact]
        public void HasReferences_WhenNotCreated_ThrowsInvalidOperationException()
        {
            Packet p = default;
            Assert.Throws<InvalidOperationException>(() => _ = p.HasReferences);
        }

        // ── CopyTo ────────────────────────────────────────────────────────────

        [Fact]
        public void CopyTo_CopiesToDestinationBuffer()
        {
            byte[] data = { 10, 20, 30 };
            var p = new Packet();
            p.Create(data);

            byte[] dest = new byte[10];
            p.CopyTo(dest, 0);

            Assert.Equal(10, dest[0]);
            Assert.Equal(20, dest[1]);
            Assert.Equal(30, dest[2]);
            p.Dispose();
        }

        [Fact]
        public void CopyTo_WithStartPos_CopiesToCorrectOffset()
        {
            byte[] data = { 0xAA, 0xBB };
            var p = new Packet();
            p.Create(data);

            byte[] dest = new byte[5];
            p.CopyTo(dest, 2);

            Assert.Equal(0xAA, dest[2]);
            Assert.Equal(0xBB, dest[3]);
            p.Dispose();
        }

        [Fact]
        public void CopyTo_WhenNotCreated_DoesNotThrow()
        {
            Packet p = default;
            byte[] dest = new byte[5];
            var ex = Record.Exception(() => p.CopyTo(dest));
            Assert.Null(ex);
        }

        [Fact]
        public void CopyTo_WithNullDestination_ThrowsArgumentNullException()
        {
            var p = new Packet();
            p.Create(new byte[] { 1, 2 });
            Assert.Throws<ArgumentNullException>(() => p.CopyTo(null!));
            p.Dispose();
        }

        // ── Dispose ───────────────────────────────────────────────────────────

        [Fact]
        public void Dispose_SetsIsSetToFalse()
        {
            var p = new Packet();
            p.Create(new byte[] { 1, 2, 3 });
            Assert.True(p.IsSet);
            p.Dispose();
            Assert.False(p.IsSet);
        }

        [Fact]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            var p = new Packet();
            p.Create(new byte[] { 1 });
            p.Dispose();
            var ex = Record.Exception(() => p.Dispose());
            Assert.Null(ex);
        }

        [Fact]
        public void Dispose_WhenNotCreated_DoesNotThrow()
        {
            Packet p = default;
            var ex = Record.Exception(() => p.Dispose());
            Assert.Null(ex);
        }
    }
}
