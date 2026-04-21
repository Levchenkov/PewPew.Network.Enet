using System;

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
    /// </summary>
    internal class ENetPacket
    {
        public ENetPacketFlag Flags;
        public byte[] Data = Array.Empty<byte>();
        public int DataLength;
        public int ReferenceCount;
        public Action<ENetPacket>? FreeCallback;
        public object? UserData;

        public static ENetPacket Create(byte[]? data, int dataLength, ENetPacketFlag flags)
        {
            var pkt = new ENetPacket
            {
                Flags = flags,
                DataLength = dataLength
            };

            if ((flags & ENetPacketFlag.NoAllocate) != 0)
            {
                // Caller manages the buffer; just reference it
                pkt.Data = data ?? Array.Empty<byte>();
            }
            else
            {
                pkt.Data = new byte[dataLength];
                if (data != null && dataLength > 0)
                    Buffer.BlockCopy(data, 0, pkt.Data, 0, dataLength);
            }

            return pkt;
        }

        public static ENetPacket CreateOffset(byte[]? data, int dataLength, int dataOffset, ENetPacketFlag flags)
        {
            int effectiveLength = dataLength - dataOffset;
            if (effectiveLength < 0) effectiveLength = 0;

            var pkt = new ENetPacket
            {
                Flags = flags,
                DataLength = effectiveLength
            };

            if ((flags & ENetPacketFlag.NoAllocate) != 0)
            {
                pkt.Data = data ?? Array.Empty<byte>();
            }
            else
            {
                pkt.Data = new byte[effectiveLength];
                if (data != null && effectiveLength > 0)
                    Buffer.BlockCopy(data, dataOffset, pkt.Data, 0, effectiveLength);
            }

            return pkt;
        }

        public void Destroy()
        {
            FreeCallback?.Invoke(this);
        }

        /// <summary>
        /// Decrement reference count; destroy when it reaches zero.
        /// Called by enet_packet_dispose in the C code.
        /// </summary>
        public void Dispose()
        {
            if (ReferenceCount > 0)
                ReferenceCount--;
            // Actual destruction is managed by reference counting in the protocol engine.
            // Explicit Destroy() is called when refCount reaches 0 after decrement.
        }
    }
}
