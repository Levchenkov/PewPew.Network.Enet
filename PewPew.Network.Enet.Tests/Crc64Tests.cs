using System;
using Xunit;
using PewPew.Network.Enet.Internal;

namespace PewPew.Network.Enet.Tests
{
    public class Crc64Tests
    {
        // ── Span overload ─────────────────────────────────────────────────────

        [Fact]
        public void Compute_EmptySpan_ReturnsZero()
        {
            ulong result = Crc64.Compute(ReadOnlySpan<byte>.Empty);
            Assert.Equal(0UL, result);
        }

        [Fact]
        public void Compute_SingleByte_IsDeterministic()
        {
            byte[] data = { 0x01 };
            ulong r1 = Crc64.Compute(data);
            ulong r2 = Crc64.Compute(data);
            Assert.Equal(r1, r2);
        }

        [Fact]
        public void Compute_DifferentData_ProducesDifferentCrcs()
        {
            ulong crc1 = Crc64.Compute(new byte[] { 0x01 });
            ulong crc2 = Crc64.Compute(new byte[] { 0x02 });
            Assert.NotEqual(crc1, crc2);
        }

        // ── Multi-buffer overload ─────────────────────────────────────────────

        [Fact]
        public void Compute_MultiBuffer_EmptyBuffers_ReturnsZero()
        {
            var buffers = new byte[][] { Array.Empty<byte>() };
            var lengths = new int[] { 0 };
            ulong result = Crc64.Compute(buffers, lengths, 1);
            Assert.Equal(0UL, result);
        }

        [Fact]
        public void Compute_MultiBuffer_MatchesSingleBuffer()
        {
            // "hello" = 0x68 0x65 0x6c 0x6c 0x6f
            byte[] hel = { 0x68, 0x65, 0x6c };
            byte[] lo = { 0x6c, 0x6f };
            byte[] hello = { 0x68, 0x65, 0x6c, 0x6c, 0x6f };

            ulong single = Crc64.Compute(hello);

            var buffers = new byte[][] { hel, lo };
            var lengths = new int[] { hel.Length, lo.Length };
            ulong multi = Crc64.Compute(buffers, lengths, 2);

            Assert.Equal(single, multi);
        }

        [Fact]
        public void Compute_MultiBuffer_ZeroBufferCount_ReturnsZero()
        {
            var buffers = new byte[][] { new byte[] { 1, 2, 3 } };
            var lengths = new int[] { 3 };
            ulong result = Crc64.Compute(buffers, lengths, 0);
            Assert.Equal(0UL, result);
        }

        [Fact]
        public void Compute_MultiBuffer_SameAsSingleBufferForSingleEntry()
        {
            byte[] data = { 10, 20, 30, 40 };
            ulong single = Crc64.Compute(data);

            var buffers = new byte[][] { data };
            var lengths = new int[] { data.Length };
            ulong multi = Crc64.Compute(buffers, lengths, 1);

            Assert.Equal(single, multi);
        }

        [Fact]
        public void Compute_NonEmptyData_ReturnsNonZero()
        {
            byte[] data = { 0xFF };
            ulong result = Crc64.Compute(data);
            Assert.NotEqual(0UL, result);
        }
    }
}
