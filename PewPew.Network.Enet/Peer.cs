using System;
using PewPew.Network.Enet.Internal;

namespace PewPew.Network.Enet
{
    public struct Peer
    {
        private ENetPeer? _peer;

        internal ENetPeer? NativePeer => _peer;

        internal Peer(ENetPeer peer)
        {
            _peer = peer;
        }

        public bool IsSet => _peer != null;

        public uint ID
        {
            get
            {
                ThrowIfNotCreated();
                return _peer!.IncomingPeerId;
            }
        }

        public string IP
        {
            get
            {
                ThrowIfNotCreated();
                return _peer!.Address?.Address.ToString() ?? string.Empty;
            }
        }

        public ushort Port
        {
            get
            {
                ThrowIfNotCreated();
                return (ushort)(_peer!.Address?.Port ?? 0);
            }
        }

        public uint MTU
        {
            get
            {
                ThrowIfNotCreated();
                return _peer!.Mtu;
            }
        }

        public PeerState State
        {
            get
            {
                if (_peer == null) return PeerState.Uninitialized;
                return (PeerState)_peer.State;
            }
        }

        public uint RoundTripTime
        {
            get
            {
                ThrowIfNotCreated();
                return _peer!.RoundTripTime;
            }
        }

        public uint LastRoundTripTime
        {
            get
            {
                ThrowIfNotCreated();
                return _peer!.LastRoundTripTime;
            }
        }

        public uint LastSendTime
        {
            get
            {
                ThrowIfNotCreated();
                return _peer!.LastSendTime;
            }
        }

        public uint LastReceiveTime
        {
            get
            {
                ThrowIfNotCreated();
                return _peer!.LastReceiveTime;
            }
        }

        public ulong PacketsSent
        {
            get
            {
                ThrowIfNotCreated();
                return _peer!.TotalPacketsSent;
            }
        }

        public ulong PacketsLost
        {
            get
            {
                ThrowIfNotCreated();
                return _peer!.TotalPacketsLost;
            }
        }

        public float PacketsThrottle
        {
            get
            {
                ThrowIfNotCreated();
                return _peer!.PacketThrottle / (float)ProtocolConstants.PeerPacketThrottleScale;
            }
        }

        public ulong BytesSent
        {
            get
            {
                ThrowIfNotCreated();
                return _peer!.TotalDataSent;
            }
        }

        public ulong BytesReceived
        {
            get
            {
                ThrowIfNotCreated();
                return _peer!.TotalDataReceived;
            }
        }

        public object? Data
        {
            get
            {
                ThrowIfNotCreated();
                return _peer!.UserData;
            }
            set
            {
                ThrowIfNotCreated();
                _peer!.UserData = value;
            }
        }

        public int Send(byte channelID, ref Packet packet)
        {
            ThrowIfNotCreated();
            packet.ThrowIfNotCreated();

            if (_peer!.Host == null) return -1;
            return _peer.Host.PeerSend(_peer, channelID, packet.NativePacket!);
        }

        public bool Receive(out byte channelID, out Packet packet)
        {
            ThrowIfNotCreated();
            if (_peer!.Host == null)
            {
                channelID = 0;
                packet = default;
                return false;
            }

            var p = _peer.Host.PeerReceive(_peer, out channelID);
            if (p != null)
            {
                packet = new Packet(p);
                return true;
            }

            packet = default;
            return false;
        }

        public void Ping()
        {
            ThrowIfNotCreated();
            _peer!.Ping();
        }

        public void PingInterval(uint interval)
        {
            ThrowIfNotCreated();
            _peer!.SetPingInterval(interval);
        }

        public void Timeout(uint timeoutLimit, uint timeoutMinimum, uint timeoutMaximum)
        {
            ThrowIfNotCreated();
            _peer!.SetTimeout(timeoutLimit, timeoutMinimum, timeoutMaximum);
        }

        public void Disconnect(uint data)
        {
            ThrowIfNotCreated();
            _peer!.Disconnect(data);
        }

        public void DisconnectNow(uint data)
        {
            ThrowIfNotCreated();
            _peer!.DisconnectNow(data);
        }

        public void DisconnectLater(uint data)
        {
            ThrowIfNotCreated();
            _peer!.DisconnectLater(data);
        }

        public void Reset()
        {
            ThrowIfNotCreated();
            _peer!.Reset();
        }

        public void ConfigureThrottle(uint interval, uint acceleration, uint deceleration, uint threshold)
        {
            ThrowIfNotCreated();
            _peer!.PacketThrottleInterval = interval;
            _peer!.PacketThrottleAcceleration = acceleration;
            _peer!.PacketThrottleDeceleration = deceleration;
            _peer!.PacketThrottleThreshold = threshold;

            if (_peer.Host == null) return;

            var cmd = new Internal.ENetProtocol();
            cmd.Header.Command = (byte)Internal.ENetProtocolCommand.ThrottleConfigure |
                                  (byte)Internal.ENetProtocolFlag.CommandFlagAcknowledge;
            cmd.Header.ChannelId = 0xFF;
            cmd.ThrottleConfigure.PacketThrottleInterval = interval;
            cmd.ThrottleConfigure.PacketThrottleAcceleration = acceleration;
            cmd.ThrottleConfigure.PacketThrottleDeceleration = deceleration;
            _peer.QueueOutgoingCommand(ref cmd, null, 0, 0);
        }

        internal void ThrowIfNotCreated()
        {
            if (_peer == null)
                throw new InvalidOperationException("Peer not created");
        }
    }
}
