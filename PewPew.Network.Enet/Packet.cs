using System;
using PewPew.Network.Enet.Internal;

namespace PewPew.Network.Enet
{
    public struct Packet : IDisposable
    {
        internal ENetPacket? NativePacket;

        internal Packet(ENetPacket pkt)
        {
            NativePacket = pkt;
        }

        public bool IsSet => NativePacket != null;

        public int Length
        {
            get
            {
                ThrowIfNotCreated();
                return NativePacket!.DataLength;
            }
        }

        public byte[] Data
        {
            get
            {
                ThrowIfNotCreated();
                return NativePacket!.Data;
            }
        }

        public bool HasReferences
        {
            get
            {
                ThrowIfNotCreated();
                return NativePacket!.ReferenceCount > 0;
            }
        }

        public void Create(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            Create(data, data.Length, PacketFlags.None);
        }

        public void Create(byte[] data, PacketFlags flags) => Create(data, data.Length, flags);

        public void Create(byte[] data, int length) => Create(data, length, PacketFlags.None);

        public void Create(byte[] data, int length, PacketFlags flags)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (length < 0 || length > data.Length) throw new ArgumentOutOfRangeException(nameof(length));

            NativePacket = ENetPacket.Create(data, length, (ENetPacketFlag)flags);
        }

        public void Create(byte[] data, int offset, int length, PacketFlags flags)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0 || length > data.Length) throw new ArgumentOutOfRangeException(nameof(length));

            NativePacket = ENetPacket.CreateOffset(data, data.Length - offset, offset, (ENetPacketFlag)flags);
        }

        public void CopyTo(byte[] destination, int startPos = 0)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (NativePacket == null || NativePacket.DataLength == 0) return;

            Buffer.BlockCopy(NativePacket.Data, 0, destination, startPos, NativePacket.DataLength);
        }

        public void SetFreeCallback(Action<Packet> callback)
        {
            ThrowIfNotCreated();
            NativePacket!.FreeCallback = p =>
            {
                callback(new Packet(p));
            };
        }

        public void Dispose()
        {
            if (NativePacket != null)
            {
                NativePacket.ReferenceCount--;
                if (NativePacket.ReferenceCount <= 0)
                    NativePacket.Destroy();
                NativePacket = null;
            }
        }

        internal void ThrowIfNotCreated()
        {
            if (NativePacket == null)
                throw new InvalidOperationException("Packet not created");
        }
    }
}
