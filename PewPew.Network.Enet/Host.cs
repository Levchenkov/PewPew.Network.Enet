using System;
using PewPew.Network.Enet.Internal;

namespace PewPew.Network.Enet
{
    public class Host : IDisposable
    {
        private ENetHostInternal? _host;
        private bool _disposed;

        public bool IsSet => _host != null;

        public uint PeersCount
        {
            get { ThrowIfNotCreated(); return _host!.PeersCount; }
        }

        public uint PacketsSent
        {
            get { ThrowIfNotCreated(); return _host!.PacketsSent; }
        }

        public uint PacketsReceived
        {
            get { ThrowIfNotCreated(); return _host!.PacketsReceived; }
        }

        public uint BytesSent
        {
            get { ThrowIfNotCreated(); return _host!.BytesSent; }
        }

        public uint BytesReceived
        {
            get { ThrowIfNotCreated(); return _host!.BytesReceived; }
        }

        // ── Create overloads ──────────────────────────────────────────────────

        public void Create() => Create(null, 1, 0);

        public void Create(int bufferSize) => Create(null, 1, 0, 0, 0, bufferSize);

        public void Create(Address? address, int peerLimit) => Create(address, peerLimit, 0);

        public void Create(Address? address, int peerLimit, int channelLimit) =>
            Create(address, peerLimit, channelLimit, 0, 0, 0);

        public void Create(int peerLimit, int channelLimit) =>
            Create(null, peerLimit, channelLimit, 0, 0, 0);

        public void Create(int peerLimit, int channelLimit, uint incomingBandwidth, uint outgoingBandwidth) =>
            Create(null, peerLimit, channelLimit, incomingBandwidth, outgoingBandwidth, 0);

        public void Create(Address? address, int peerLimit, int channelLimit,
                           uint incomingBandwidth, uint outgoingBandwidth) =>
            Create(address, peerLimit, channelLimit, incomingBandwidth, outgoingBandwidth, 0);

        public void Create(Address? address, int peerLimit, int channelLimit,
                           uint incomingBandwidth, uint outgoingBandwidth, int bufferSize)
        {
            if (_host != null)
                throw new InvalidOperationException("Host already created");

            if (peerLimit < 0 || peerLimit > Library.MaxPeers)
                throw new ArgumentOutOfRangeException(nameof(peerLimit));

            if (channelLimit < 0 || channelLimit > Library.MaxChannelCount)
                throw new ArgumentOutOfRangeException(nameof(channelLimit));

            _host = new ENetHostInternal();
            _host.Create(address, peerLimit, channelLimit,
                          incomingBandwidth, outgoingBandwidth, bufferSize);
        }

        public void PreventConnections(bool state)
        {
            ThrowIfNotCreated();
            _host!.PreventConnections = state;
        }

        public void Broadcast(byte channelID, ref Packet packet)
        {
            ThrowIfNotCreated();
            packet.ThrowIfNotCreated();
            _host!.Broadcast(channelID, packet.NativePacket!);
            packet.NativePacket = null;
        }

        public void Broadcast(byte channelID, ref Packet packet, Peer excludedPeer)
        {
            ThrowIfNotCreated();
            packet.ThrowIfNotCreated();
            if (excludedPeer.NativePeer != null)
                _host!.BroadcastExclude(channelID, packet.NativePacket!, excludedPeer.NativePeer);
            else
                _host!.Broadcast(channelID, packet.NativePacket!);
            packet.NativePacket = null;
        }

        public void Broadcast(byte channelID, ref Packet packet, Peer[] peers)
        {
            if (peers == null) throw new ArgumentNullException(nameof(peers));
            ThrowIfNotCreated();
            packet.ThrowIfNotCreated();

            if (peers.Length > 0)
            {
                var nativePeers = new ENetPeer[peers.Length];
                int count = 0;
                foreach (var p in peers)
                {
                    if (p.NativePeer != null)
                        nativePeers[count++] = p.NativePeer;
                }

                if (count > 0)
                {
                    Array.Resize(ref nativePeers, count);
                    _host!.BroadcastSelective(channelID, packet.NativePacket!, nativePeers);
                }
            }

            packet.NativePacket = null;
        }

        public int CheckEvents(out Event @event)
        {
            ThrowIfNotCreated();

            var nativeEvent = new ENetEvent();
            int result = _host!.CheckEvents(nativeEvent);

            if (result <= 0)
            {
                @event = default;
                return result;
            }

            @event = new Event(nativeEvent);
            return result;
        }

        public Peer Connect(Address address)
        {
            return Connect(address, 0, 0);
        }

        public Peer Connect(Address address, int channelLimit)
        {
            return Connect(address, channelLimit, 0);
        }

        public Peer Connect(Address address, int channelLimit, uint data)
        {
            ThrowIfNotCreated();

            if (channelLimit < 0 || channelLimit > Library.MaxChannelCount)
                throw new ArgumentOutOfRangeException(nameof(channelLimit));

            var nativePeer = _host!.Connect(address, channelLimit, data);
            if (nativePeer == null)
                throw new InvalidOperationException("Host connect call failed");

            return new Peer(nativePeer);
        }

        public int Service(int timeout, out Event @event)
        {
            if (timeout < 0) throw new ArgumentOutOfRangeException(nameof(timeout));
            ThrowIfNotCreated();

            var nativeEvent = new ENetEvent();
            int result = _host!.Service(nativeEvent, (uint)timeout);

            if (result <= 0)
            {
                @event = default;
                return result;
            }

            @event = new Event(nativeEvent);
            return result;
        }

        public void SetBandwidthLimit(uint incomingBandwidth, uint outgoingBandwidth)
        {
            ThrowIfNotCreated();
            _host!.SetBandwidthLimit(incomingBandwidth, outgoingBandwidth);
        }

        public void SetChannelLimit(int channelLimit)
        {
            ThrowIfNotCreated();
            if (channelLimit < 0 || channelLimit > Library.MaxChannelCount)
                throw new ArgumentOutOfRangeException(nameof(channelLimit));
            _host!.SetChannelLimit(channelLimit);
        }

        public void SetMaxDuplicatePeers(ushort number)
        {
            ThrowIfNotCreated();
            _host!.DuplicatePeers = number;
        }

        public void SetChecksumCallback(Func<byte[][], int[], int, ulong>? callback)
        {
            ThrowIfNotCreated();
            _host!.ChecksumCallback = callback;
        }

        public void Flush()
        {
            ThrowIfNotCreated();
            _host!.Flush();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _host?.Dispose();
                _host = null;
                _disposed = true;
            }
        }

        ~Host() => Dispose(false);

        internal void ThrowIfNotCreated()
        {
            if (_host == null)
                throw new InvalidOperationException("Host not created");
        }
    }
}
