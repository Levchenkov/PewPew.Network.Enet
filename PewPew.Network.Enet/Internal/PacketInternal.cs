using System;
using System.Buffers;
using System.Collections.Generic;

namespace PewPew.Network.Enet.Internal
{
    [Flags]
    internal enum ENetPacketFlag : uint
    {
        None = 0,
        Reliable = 1 << 0,
        Unsequenced = 1 << 1,
        NoAllocate = 1 << 2,
        UnreliableFragmented = 1 << 3,
        Instant = 1 << 4,
        Unthrottled = 1 << 5,
        Sent = 1 << 8
    }

    /// <summary>
    /// Internal managed equivalent of ENetPacket. Reference-counted.
    /// Data buffers are rented from <see cref="ArrayPool{T}"/> to avoid per-packet heap allocations.
    /// </summary>
    internal class ENetPacket
    {
        private const int PacketPoolMax = 512;
        private static readonly Stack<ENetPacket> _packetPool = new(64);

        public ENetPacketFlag Flags;
        public byte[] Data = Array.Empty<byte>();
        /// <summary>Logical length of valid data in <see cref="Data"/>.</summary>
        public int DataLength;
        public int ReferenceCount;
        public Action<ENetPacket>? FreeCallback;
        public object? UserData;

        // Tracks whether Data was rented from ArrayPool (must be returned on Destroy).
        private bool _fromPool;

        // ── Factory ──────────────────────────────────────────────────────────

        public static ENetPacket Create(byte[]? data, int dataLength, ENetPacketFlag flags)
        {
            var pkt = Allocate();
            pkt.Flags = flags;
            pkt.DataLength = dataLength;

            if ((flags & ENetPacketFlag.NoAllocate) != 0)
            {
                pkt.Data = data ?? Array.Empty<byte>();
                pkt._fromPool = false;
            }
            else
            {
                pkt.Data = dataLength > 0 ? ArrayPool<byte>.Shared.Rent(dataLength) : Array.Empty<byte>();
                pkt._fromPool = dataLength > 0;
                if (data != null && dataLength > 0)
                    Buffer.BlockCopy(data, 0, pkt.Data, 0, dataLength);
            }

            return pkt;
        }

        public static ENetPacket CreateOffset(byte[]? data, int dataLength, int dataOffset, ENetPacketFlag flags)
        {
            int effectiveLength = Math.Max(0, dataLength - dataOffset);
            var pkt = Allocate();
            pkt.Flags = flags;
            pkt.DataLength = effectiveLength;

            if ((flags & ENetPacketFlag.NoAllocate) != 0)
            {
                pkt.Data = data ?? Array.Empty<byte>();
                pkt._fromPool = false;
            }
            else
            {
                pkt.Data = effectiveLength > 0 ? ArrayPool<byte>.Shared.Rent(effectiveLength) : Array.Empty<byte>();
                pkt._fromPool = effectiveLength > 0;
                if (data != null && effectiveLength > 0)
                    Buffer.BlockCopy(data, dataOffset, pkt.Data, 0, effectiveLength);
            }

            return pkt;
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        public void Destroy()
        {
            FreeCallback?.Invoke(this);
            ReturnBuffer();
            ReturnToPool(this);
        }

        /// <summary>
        /// Decrement reference count; actual destruction is handled externally
        /// when the count reaches zero.
        /// </summary>
        public void Dispose()
        {
            if (ReferenceCount > 0)
                ReferenceCount--;
        }

        // ── Pool helpers ──────────────────────────────────────────────────────

        private static ENetPacket Allocate()
            => _packetPool.TryPop(out var pkt) ? pkt : new ENetPacket();

        private void ReturnBuffer()
        {
            if (_fromPool && Data.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(Data);
                Data = Array.Empty<byte>();
                _fromPool = false;
            }
        }

        private static void ReturnToPool(ENetPacket pkt)
        {
            if (_packetPool.Count >= PacketPoolMax) return;
            pkt.DataLength = 0;
            pkt.ReferenceCount = 0;
            pkt.FreeCallback = null;
            pkt.UserData = null;
            pkt.Flags = ENetPacketFlag.None;
            _packetPool.Push(pkt);
        }
    }
}
