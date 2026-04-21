using System;
using System.Net;
using System.Net.Sockets;

namespace PewPew.Network.Enet.Internal
{
    // =========================================================================
    // Event types for internal use
    // =========================================================================

    internal enum ENetEventType
    {
        None = 0,
        Connect = 1,
        Disconnect = 2,
        Receive = 3,
        DisconnectTimeout = 4
    }

    internal class ENetEvent
    {
        public ENetEventType Type;
        public ENetPeer? Peer;
        public byte ChannelId;
        public uint Data;
        public ENetPacket? Packet;
    }

    // =========================================================================
    // Host internal
    // =========================================================================

    internal class ENetHostInternal : IDisposable
    {
        private readonly UdpSocket _socket = new();
        private bool _disposed;

        // Object pool — eliminates per-packet allocations for command objects
        public readonly ENetCommandPool Pool = new();

        // ── Peers ─────────────────────────────────────────────────────────────
        public ENetPeer[] Peers = Array.Empty<ENetPeer>();
        public int PeerCount;
        public int ChannelLimit;
        public int ConnectedPeers;
        public int BandwidthLimitedPeers;
        public int DuplicatePeers = ProtocolConstants.MaximumPeerId;

        // ── Address ───────────────────────────────────────────────────────────
        public IPEndPoint? Address;

        // ── Bandwidth ─────────────────────────────────────────────────────────
        public uint IncomingBandwidth;
        public uint OutgoingBandwidth;
        public uint BandwidthThrottleEpoch;
        public uint Mtu;
        public uint RandomSeed;
        public bool RecalculateBandwidthLimits;
        public bool PreventConnections;

        // ── Service state ─────────────────────────────────────────────────────
        public uint ServiceTime;
        public readonly ENetList<ENetPeer> DispatchQueue = new();
        public bool ContinueSending;
        public ulong TotalSentData;
        public uint TotalSentPackets;
        public uint TotalReceivedData;
        public uint TotalReceivedPackets;

        // ── Receive buffer ────────────────────────────────────────────────────
        private readonly byte[] _receiveBuffer = new byte[ProtocolConstants.MaximumMtu];
        private int _receivedDataLength;
        private IPEndPoint? _receivedAddress;

        // ── Callbacks ─────────────────────────────────────────────────────────
        public Func<byte[][], int[], int, ulong>? ChecksumCallback;
        public Func<ENetEvent, IPEndPoint, byte[], int, int>? InterceptCallback;

        // ── Max sizes ─────────────────────────────────────────────────────────
        public int MaximumPacketSize = ProtocolConstants.HostDefaultMaxPacketSize;
        public int MaximumWaitingData = ProtocolConstants.HostDefaultMaxWaitingData;

        // ─────────────────────────────────────────────────────────────────────
        // Creation / destruction
        // ─────────────────────────────────────────────────────────────────────

        public void Create(Address? bindAddress, int peerCount, int channelLimit,
                           uint incomingBandwidth, uint outgoingBandwidth, int bufferSize)
        {
            if (peerCount > ProtocolConstants.MaximumPeerId)
                peerCount = ProtocolConstants.MaximumPeerId;

            PeerCount = peerCount;
            ChannelLimit = channelLimit == 0 || channelLimit > ProtocolConstants.MaximumChannelCount
                ? ProtocolConstants.MaximumChannelCount
                : channelLimit;

            IncomingBandwidth = incomingBandwidth;
            OutgoingBandwidth = outgoingBandwidth;
            BandwidthThrottleEpoch = 0;
            RecalculateBandwidthLimits = false;
            ConnectedPeers = 0;
            BandwidthLimitedPeers = 0;
            DuplicatePeers = ProtocolConstants.MaximumPeerId;
            ServiceTime = 0;
            ContinueSending = false;
            TotalSentData = 0;
            TotalSentPackets = 0;
            TotalReceivedData = 0;
            TotalReceivedPackets = 0;

            RandomSeed = (uint)Environment.TickCount ^ (uint)(System.Threading.Thread.CurrentThread.ManagedThreadId << 16);
            Mtu = ProtocolConstants.HostDefaultMtu;

            // Initialise peers — pre-allocate channels up to ChannelLimit so Connect()
            // can reuse them via Reset() instead of allocating on every new connection.
            Peers = new ENetPeer[peerCount];
            for (int i = 0; i < peerCount; i++)
            {
                var peer = new ENetPeer { Host = this, IncomingPeerId = (ushort)i };
                peer.Channels = new ENetChannel[ChannelLimit];
                for (int c = 0; c < ChannelLimit; c++)
                    peer.Channels[c] = new ENetChannel();
                Peers[i] = peer;
            }

            // Create socket
            int rxBuf = bufferSize > 0 ? bufferSize : ProtocolConstants.HostBufferSizeMax;
            int txBuf = bufferSize > 0 ? bufferSize : ProtocolConstants.HostBufferSizeMax;
            _socket.Create(bindAddress, rxBuf, txBuf);

            if (bindAddress.HasValue)
                Address = bindAddress.Value.ToEndPoint();
        }

        public void Destroy()
        {
            if (Peers != null)
            {
                foreach (var peer in Peers)
                    peer.Reset();
            }
            _socket.Dispose();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Initiate a connection to a remote host.
        /// Mirrors enet_host_connect().
        /// </summary>
        public ENetPeer? Connect(Address remoteAddress, int channelCount, uint data)
        {
            if (channelCount < ProtocolConstants.MinimumChannelCount)
                channelCount = ProtocolConstants.MinimumChannelCount;
            if (channelCount > ProtocolConstants.MaximumChannelCount)
                channelCount = ProtocolConstants.MaximumChannelCount;

            ENetPeer? currentPeer = null;
            foreach (var p in Peers)
            {
                if (p.State == ENetPeerState.Disconnected)
                {
                    currentPeer = p;
                    break;
                }
            }

            if (currentPeer == null) return null;

            // Reuse the pre-allocated channel array; just reset the channels we'll use.
            // If the peer somehow lost its channel array (shouldn't happen), re-create it.
            if (currentPeer.Channels == null || currentPeer.Channels.Length < channelCount)
            {
                currentPeer.Channels = new ENetChannel[Math.Max(channelCount, ChannelLimit)];
                for (int i = 0; i < currentPeer.Channels.Length; i++)
                    currentPeer.Channels[i] = new ENetChannel();
            }
            for (int i = 0; i < channelCount; i++)
                currentPeer.Channels[i].Reset();
            currentPeer.ChannelCount = channelCount;
            currentPeer.State = ENetPeerState.Connecting;
            currentPeer.Address = remoteAddress.ToEndPoint();
            currentPeer.ConnectId = ++RandomSeed;
            currentPeer.OutgoingSessionId = 0xFF;
            currentPeer.IncomingSessionId = 0xFF;
            currentPeer.Mtu = Mtu;
            currentPeer.WindowSize = (uint)Math.Max(
                ProtocolConstants.MinimumWindowSize,
                Math.Min(ProtocolConstants.MaximumWindowSize,
                         (int)(OutgoingBandwidth / ProtocolConstants.PeerWindowSizeScale * ProtocolConstants.PeerDefaultRoundTripTime)));

            currentPeer.PacketThrottleInterval = ProtocolConstants.PeerPacketThrottleInterval;
            currentPeer.PacketThrottleAcceleration = ProtocolConstants.PeerPacketThrottleAcceleration;
            currentPeer.PacketThrottleDeceleration = ProtocolConstants.PeerPacketThrottleDeceleration;
            currentPeer.EventData = data;
            currentPeer.IncomingBandwidth = IncomingBandwidth;
            currentPeer.OutgoingBandwidth = OutgoingBandwidth;

            // Build connect command
            var cmd = new ENetProtocol();
            cmd.Header.Command = (byte)ENetProtocolCommand.Connect |
                                  (byte)ENetProtocolFlag.CommandFlagAcknowledge;
            cmd.Header.ChannelId = 0xFF;
            cmd.Connect.OutgoingPeerId = currentPeer.IncomingPeerId;
            cmd.Connect.IncomingSessionId = currentPeer.IncomingSessionId;
            cmd.Connect.OutgoingSessionId = currentPeer.OutgoingSessionId;
            cmd.Connect.Mtu = currentPeer.Mtu;
            cmd.Connect.WindowSize = currentPeer.WindowSize;
            cmd.Connect.ChannelCount = (uint)channelCount;
            cmd.Connect.IncomingBandwidth = IncomingBandwidth;
            cmd.Connect.OutgoingBandwidth = OutgoingBandwidth;
            cmd.Connect.PacketThrottleInterval = currentPeer.PacketThrottleInterval;
            cmd.Connect.PacketThrottleAcceleration = currentPeer.PacketThrottleAcceleration;
            cmd.Connect.PacketThrottleDeceleration = currentPeer.PacketThrottleDeceleration;
            cmd.Connect.ConnectId = currentPeer.ConnectId;
            cmd.Connect.Data = data;

            currentPeer.QueueOutgoingCommand(ref cmd, null, 0, 0);
            return currentPeer;
        }

        /// <summary>
        /// Service pending events: receive data, dispatch events.
        /// Returns >0 if event was dispatched, 0 if none, <0 on error.
        /// Mirrors enet_host_service().
        /// </summary>
        public int Service(ENetEvent? netEvent, uint timeout)
        {
            if (netEvent != null)
            {
                netEvent.Type = ENetEventType.None;
                netEvent.Peer = null;
                netEvent.Packet = null;
            }

            // Check if there's already a queued dispatch
            if (DispatchIncomingCommands(netEvent) > 0)
                return 1;

            ServiceTime = TimeUtils.GetTime();

            do
            {
                if (TimeUtils.TimeGreaterEqual(ServiceTime, BandwidthThrottleEpoch + ProtocolConstants.HostBandwidthThrottleInterval))
                    BandwidthThrottle();

                SendOutgoingCommands(netEvent, true);

                // Receive
                int received = ReceiveIncomingCommands(netEvent);
                if (received < 0) return -1;
                if (received > 0) return 1;

                if (DispatchIncomingCommands(netEvent) > 0)
                    return 1;

                ServiceTime = TimeUtils.GetTime();

                if (timeout == 0) break;

                // Wait for data or timeout
                if (!_socket.IsValid) break;
                uint condition = 2; // receive
                int pollResult = _socket.Wait(ref condition, timeout * 1000UL);
                if (pollResult < 0) return -1;
                if ((condition & 2) == 0) break; // no data arrived

                ServiceTime = TimeUtils.GetTime();
                timeout = 0; // only wait once
            }
            while (true);

            return 0;
        }

        /// <summary>
        /// Check for queued events without blocking.
        /// </summary>
        public int CheckEvents(ENetEvent? netEvent)
        {
            if (netEvent == null) return -1;
            netEvent.Type = ENetEventType.None;
            netEvent.Peer = null;
            netEvent.Packet = null;
            return DispatchIncomingCommands(netEvent);
        }

        public void Flush()
        {
            ServiceTime = TimeUtils.GetTime();
            SendOutgoingCommands(null, false);
        }

        public void Broadcast(byte channelId, ENetPacket packet)
        {
            foreach (var peer in Peers)
            {
                if (peer.State != ENetPeerState.Connected) continue;
                var cmd = BuildSendCommand(peer, channelId, packet);
                peer.QueueOutgoingCommand(ref cmd, packet, 0, (ushort)packet.DataLength);
            }
        }

        public void BroadcastExclude(byte channelId, ENetPacket packet, ENetPeer excluded)
        {
            foreach (var peer in Peers)
            {
                if (peer.State != ENetPeerState.Connected || peer == excluded) continue;
                var cmd = BuildSendCommand(peer, channelId, packet);
                peer.QueueOutgoingCommand(ref cmd, packet, 0, (ushort)packet.DataLength);
            }
        }

        public void BroadcastSelective(byte channelId, ENetPacket packet, ENetPeer[] targets)
        {
            foreach (var target in targets)
            {
                if (target.State != ENetPeerState.Connected) continue;
                var cmd = BuildSendCommand(target, channelId, packet);
                target.QueueOutgoingCommand(ref cmd, packet, 0, (ushort)packet.DataLength);
            }
        }

        public void SetChannelLimit(int limit)
        {
            ChannelLimit = limit == 0 || limit > ProtocolConstants.MaximumChannelCount
                ? ProtocolConstants.MaximumChannelCount
                : limit;
        }

        public void SetBandwidthLimit(uint incoming, uint outgoing)
        {
            IncomingBandwidth = incoming;
            OutgoingBandwidth = outgoing;
            RecalculateBandwidthLimits = true;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Peer send
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Send a packet to a specific peer. Mirrors enet_peer_send().</summary>
        public int PeerSend(ENetPeer peer, byte channelId, ENetPacket packet)
        {
            if (peer.State != ENetPeerState.Connected) return -1;
            if (channelId >= peer.ChannelCount) return -1;
            if (packet.DataLength > MaximumPacketSize) return -1;

            var channel = peer.Channels![channelId];

            int fragmentLength = (int)peer.Mtu - CommandSizes.ProtocolHeaderSize - CommandSizes.GetCommandSize(
                (packet.Flags & ENetPacketFlag.Reliable) != 0
                    ? (byte)ENetProtocolCommand.SendReliable
                    : (byte)ENetProtocolCommand.SendUnreliable);

            if (fragmentLength < 0) fragmentLength = 1;

            if ((packet.Flags & ENetPacketFlag.None) == ENetPacketFlag.None &&
                (packet.Flags & ENetPacketFlag.Reliable) == 0 &&
                (packet.Flags & ENetPacketFlag.UnreliableFragmented) == 0 &&
                packet.DataLength > fragmentLength)
                return -1;

            bool fragmented = packet.DataLength > fragmentLength;
            bool unreliableFragmented = (packet.Flags & ENetPacketFlag.UnreliableFragmented) != 0;

            if (!fragmented || (unreliableFragmented && (packet.Flags & ENetPacketFlag.Reliable) == 0))
            {
                // Single packet
                var cmd = new ENetProtocol();
                if ((packet.Flags & ENetPacketFlag.Reliable) != 0 ||
                    (packet.Flags & ENetPacketFlag.Unsequenced) != 0)
                {
                    if ((packet.Flags & ENetPacketFlag.Unsequenced) != 0)
                    {
                        cmd.Header.Command = (byte)ENetProtocolCommand.SendUnsequenced |
                                             (byte)ENetProtocolFlag.CommandFlagUnsequenced;
                        cmd.SendUnsequenced.DataLength = (ushort)packet.DataLength;
                    }
                    else
                    {
                        cmd.Header.Command = (byte)ENetProtocolCommand.SendReliable |
                                             (byte)ENetProtocolFlag.CommandFlagAcknowledge;
                        cmd.SendReliable.DataLength = (ushort)packet.DataLength;
                    }
                }
                else
                {
                    if (unreliableFragmented)
                    {
                        cmd.Header.Command = (byte)ENetProtocolCommand.SendUnreliableFragment |
                                             (byte)ENetProtocolFlag.CommandFlagAcknowledge;
                        cmd.SendFragment.DataLength = (ushort)packet.DataLength;
                    }
                    else
                    {
                        cmd.Header.Command = (byte)ENetProtocolCommand.SendUnreliable;
                        cmd.SendUnreliable.DataLength = (ushort)packet.DataLength;
                    }
                }
                cmd.Header.ChannelId = channelId;

                if (peer.QueueOutgoingCommand(ref cmd, packet, 0, (ushort)packet.DataLength) == null)
                    return -1;
            }
            else
            {
                // Fragment the packet
                if (!FragmentAndQueue(peer, channelId, packet, fragmentLength))
                    return -1;
            }

            return 0;
        }

        private bool FragmentAndQueue(ENetPeer peer, byte channelId, ENetPacket packet, int fragmentLength)
        {
            int totalLength = packet.DataLength;
            int fragmentCount = (totalLength + fragmentLength - 1) / fragmentLength;

            if (fragmentCount > ProtocolConstants.MaximumFragmentCount)
                return false;

            bool isReliable = (packet.Flags & ENetPacketFlag.Reliable) != 0;
            byte baseCmd = isReliable
                ? (byte)ENetProtocolCommand.SendFragment
                : (byte)ENetProtocolCommand.SendUnreliableFragment;

            ushort startSequenceNumber = (ushort)(peer.Channels![channelId].OutgoingReliableSequenceNumber + 1);
            int offset = 0;

            for (int i = 0; i < fragmentCount; i++)
            {
                int fragLen = Math.Min(fragmentLength, totalLength - offset);

                var cmd = new ENetProtocol();
                cmd.Header.Command = isReliable
                    ? (byte)(baseCmd | (byte)ENetProtocolFlag.CommandFlagAcknowledge)
                    : baseCmd;
                cmd.Header.ChannelId = channelId;
                cmd.SendFragment.StartSequenceNumber = startSequenceNumber;
                cmd.SendFragment.DataLength = (ushort)fragLen;
                cmd.SendFragment.FragmentCount = (uint)fragmentCount;
                cmd.SendFragment.FragmentNumber = (uint)i;
                cmd.SendFragment.TotalLength = (uint)totalLength;
                cmd.SendFragment.FragmentOffset = (uint)offset;

                if (peer.QueueOutgoingCommand(ref cmd, packet, (uint)offset, (ushort)fragLen) == null)
                    return false;

                offset += fragLen;
            }

            return true;
        }

        private ENetProtocol BuildSendCommand(ENetPeer peer, byte channelId, ENetPacket packet)
        {
            var cmd = new ENetProtocol();
            cmd.Header.ChannelId = channelId;
            if ((packet.Flags & ENetPacketFlag.Reliable) != 0)
            {
                cmd.Header.Command = (byte)ENetProtocolCommand.SendReliable |
                                     (byte)ENetProtocolFlag.CommandFlagAcknowledge;
                cmd.SendReliable.DataLength = (ushort)packet.DataLength;
            }
            else if ((packet.Flags & ENetPacketFlag.Unsequenced) != 0)
            {
                cmd.Header.Command = (byte)ENetProtocolCommand.SendUnsequenced |
                                     (byte)ENetProtocolFlag.CommandFlagUnsequenced;
                cmd.SendUnsequenced.DataLength = (ushort)packet.DataLength;
            }
            else
            {
                cmd.Header.Command = (byte)ENetProtocolCommand.SendUnreliable;
                cmd.SendUnreliable.DataLength = (ushort)packet.DataLength;
            }
            return cmd;
        }

        /// <summary>
        /// Receive a packet from a peer channel. Mirrors enet_peer_receive().
        /// </summary>
        public ENetPacket? PeerReceive(ENetPeer peer, out byte channelId)
        {
            channelId = 0;
            if (peer.DispatchedCommands.IsEmpty) return null;

            var node = peer.DispatchedCommands.Begin;
            var cmd = node.Owner!;
            channelId = cmd.Command.Header.ChannelId;

            peer.DispatchedCommands.Remove(node);

            var packet = cmd.Packet;
            if (packet != null)
            {
                peer.TotalWaitingData -= packet.DataLength;
            }

            Pool.ReturnIncoming(cmd);
            return packet;
        }

        /// <summary>
        /// Handle graceful / immediate / deferred disconnect.
        /// </summary>
        public void PeerDisconnect(ENetPeer peer, uint data, bool now, bool later)
        {
            if (now)
            {
                if (peer.State == ENetPeerState.Connected ||
                    peer.State == ENetPeerState.DisconnectLater)
                {
                    // Queue disconnect command first
                    var cmd = new ENetProtocol();
                    cmd.Header.Command = (byte)ENetProtocolCommand.Disconnect;
                    cmd.Header.ChannelId = 0xFF;
                    cmd.Disconnect.Data = data;
                    peer.QueueOutgoingCommand(ref cmd, null, 0, 0);
                    Flush();
                }
                peer.Reset();
                return;
            }

            if (later && !peer.OutgoingCommands.IsEmpty && !peer.SentReliableCommands.IsEmpty)
            {
                peer.State = ENetPeerState.DisconnectLater;
                peer.EventData = data;
                return;
            }

            // Normal disconnect
            if (peer.State == ENetPeerState.Connected ||
                peer.State == ENetPeerState.DisconnectLater)
            {
                var cmd = new ENetProtocol();
                cmd.Header.Command = (byte)ENetProtocolCommand.Disconnect |
                                     (byte)ENetProtocolFlag.CommandFlagAcknowledge;
                cmd.Header.ChannelId = 0xFF;
                cmd.Disconnect.Data = data;
                peer.State = ENetPeerState.Disconnecting;
                peer.QueueOutgoingCommand(ref cmd, null, 0, 0);
            }
            else
            {
                peer.Reset();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Dispatch incoming commands (drive connect/disconnect/receive events)
        // ─────────────────────────────────────────────────────────────────────

        private int DispatchIncomingCommands(ENetEvent? evt)
        {
            while (!DispatchQueue.IsEmpty)
            {
                var node = DispatchQueue.Begin;
                var peer = node.Owner!;
                DispatchQueue.Remove(node);
                peer.NeedsDispatch = false;

                switch (peer.State)
                {
                    case ENetPeerState.ConnectionPending:
                    case ENetPeerState.ConnectionSucceeded:
                        ChangeState(peer, ENetPeerState.Connected);
                        if (evt != null)
                        {
                            evt.Type = ENetEventType.Connect;
                            evt.Peer = peer;
                            evt.Data = peer.EventData;
                            return 1;
                        }
                        break;

                    case ENetPeerState.Zombie:
                        RecalculateBandwidthLimits = true;
                        if (evt != null)
                        {
                            evt.Type = ENetEventType.Disconnect;
                            evt.Peer = peer;
                            evt.Data = peer.EventData;
                            peer.Reset();
                            return 1;
                        }
                        break;

                    case ENetPeerState.Connected:
                        if (peer.DispatchedCommands.IsEmpty) continue;

                        var packet = PeerReceive(peer, out byte chanId);
                        if (packet == null) continue;

                        if (evt != null)
                        {
                            evt.Type = ENetEventType.Receive;
                            evt.Peer = peer;
                            evt.ChannelId = chanId;
                            evt.Packet = packet;

                            if (!peer.DispatchedCommands.IsEmpty)
                            {
                                peer.NeedsDispatch = true;
                                DispatchQueue.Insert(DispatchQueue.End, peer.DispatchListNode);
                            }
                            return 1;
                        }
                        break;
                }
            }

            return 0;
        }

        private void ChangeState(ENetPeer peer, ENetPeerState state)
        {
            if (state == ENetPeerState.Connected || state == ENetPeerState.DisconnectLater)
                peer.OnConnect();
            else
                peer.OnDisconnect();
            peer.State = state;
        }

        private void DispatchState(ENetPeer peer, ENetPeerState state)
        {
            ChangeState(peer, state);
            if (!peer.NeedsDispatch)
            {
                DispatchQueue.Insert(DispatchQueue.End, peer.DispatchListNode);
                peer.NeedsDispatch = true;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Receive incoming data from socket
        // ─────────────────────────────────────────────────────────────────────

        private int ReceiveIncomingCommands(ENetEvent? evt)
        {
            for (int i = 0; i < 256; i++)
            {
                int received = _socket.ReceiveFrom(_receiveBuffer, 0, _receiveBuffer.Length, out _receivedAddress);

                if (received == 0) return 0;
                if (received < 0) return -1;

                _receivedDataLength = received;
                TotalReceivedData += (uint)received;
                TotalReceivedPackets++;

                int result = HandleIncomingCommands(evt);
                if (result == 1) return 1;
            }

            return 0;
        }

        private int HandleIncomingCommands(ENetEvent? evt)
        {
            var data = new ReadOnlySpan<byte>(_receiveBuffer, 0, _receivedDataLength);
            if (data.Length < 2) return -1;

            var header = ProtocolHeader.Read(data);
            bool hasSentTime = (header.PeerId & (ushort)ENetProtocolFlag.HeaderFlagSentTime) != 0;
            ushort peerId = (ushort)(header.PeerId & ~(ushort)ENetProtocolFlag.HeaderFlagMask & ~(ushort)ENetProtocolFlag.HeaderSessionMask);
            int sessionId = (header.PeerId & (ushort)ENetProtocolFlag.HeaderSessionMask) >> (int)ENetProtocolFlag.HeaderSessionShift;

            int headerSize = hasSentTime ? 4 : 2;
            ushort sentTime = hasSentTime ? header.SentTime : (ushort)0;

            if (data.Length < headerSize) return -1;

            ENetPeer? peer = null;
            if (peerId == ProtocolConstants.MaximumPeerId)
            {
                peer = null;
            }
            else if (peerId >= PeerCount)
            {
                return -1;
            }
            else
            {
                peer = Peers[peerId];
                if (peer.State == ENetPeerState.Disconnected ||
                    peer.State == ENetPeerState.Zombie ||
                    _receivedAddress == null ||
                    (peer.Address != null && !peer.Address.Equals(_receivedAddress) &&
                     peer.State != ENetPeerState.Connecting))
                {
                    return 0;
                }
                if (peer.State != ENetPeerState.Connecting &&
                    sessionId != ((peer.IncomingSessionId - 1) & 3))
                    return 0;
            }

            if (peer != null)
            {
                peer.Address = _receivedAddress;
                peer.IncomingDataTotal += (uint)_receivedDataLength;
                peer.TotalDataReceived += (uint)_receivedDataLength;
            }

            int offset = headerSize;
            while (offset < data.Length)
            {
                if (offset + CommandSizes.CommandHeaderSize > data.Length) break;

                byte commandByte = data[offset];
                ENetProtocolCommand cmdType = (ENetProtocolCommand)(commandByte & (byte)ENetProtocolCommand.Mask);

                int cmdSize = CommandSizes.GetCommandSize(commandByte);
                if (cmdSize == 0 || offset + cmdSize > data.Length) break;

                var cmdSpan = data.Slice(offset, cmdSize);
                var protocol = ReadCommand(cmdType, cmdSpan);

                offset += cmdSize;

                if (peer == null && cmdType != ENetProtocolCommand.Connect)
                    break;

                int result = HandleCommand(evt, peer, ref protocol, sentTime, cmdType, cmdSpan, data.Slice(offset));
                if (result == 1) return 1;
            }

            return 0;
        }

        private ENetProtocol ReadCommand(ENetProtocolCommand cmdType, ReadOnlySpan<byte> span)
        {
            var p = new ENetProtocol();
            p.Header = ProtocolCommandHeader.Read(span);

            switch (cmdType)
            {
                case ENetProtocolCommand.Acknowledge:
                    p.Acknowledge = ProtocolAcknowledge.Read(span); break;
                case ENetProtocolCommand.Connect:
                    p.Connect = ProtocolConnect.Read(span); break;
                case ENetProtocolCommand.VerifyConnect:
                    p.VerifyConnect = ProtocolVerifyConnect.Read(span); break;
                case ENetProtocolCommand.Disconnect:
                    p.Disconnect = ProtocolDisconnect.Read(span); break;
                case ENetProtocolCommand.Ping:
                    p.Ping = ProtocolPing.Read(span); break;
                case ENetProtocolCommand.SendReliable:
                    p.SendReliable = ProtocolSendReliable.Read(span); break;
                case ENetProtocolCommand.SendUnreliable:
                    p.SendUnreliable = ProtocolSendUnreliable.Read(span); break;
                case ENetProtocolCommand.SendUnsequenced:
                    p.SendUnsequenced = ProtocolSendUnsequenced.Read(span); break;
                case ENetProtocolCommand.SendFragment:
                case ENetProtocolCommand.SendUnreliableFragment:
                    p.SendFragment = ProtocolSendFragment.Read(span); break;
                case ENetProtocolCommand.BandwidthLimit:
                    p.BandwidthLimit = ProtocolBandwidthLimit.Read(span); break;
                case ENetProtocolCommand.ThrottleConfigure:
                    p.ThrottleConfigure = ProtocolThrottleConfigure.Read(span); break;
            }

            return p;
        }

        private int HandleCommand(ENetEvent? evt, ENetPeer? peer, ref ENetProtocol command,
            ushort sentTime, ENetProtocolCommand cmdType, ReadOnlySpan<byte> cmdSpan,
            ReadOnlySpan<byte> remaining)
        {
            bool needsAck = (command.Header.Command & (byte)ENetProtocolFlag.CommandFlagAcknowledge) != 0;

            switch (cmdType)
            {
                case ENetProtocolCommand.Acknowledge:
                    if (peer != null) HandleAcknowledge(peer, ref command);
                    break;

                case ENetProtocolCommand.Connect:
                    peer = HandleConnect(ref command);
                    break;

                case ENetProtocolCommand.VerifyConnect:
                    if (peer != null)
                        return HandleVerifyConnect(evt, peer, ref command);
                    break;

                case ENetProtocolCommand.Disconnect:
                    if (peer != null)
                        return HandleDisconnect(evt, peer, ref command);
                    break;

                case ENetProtocolCommand.Ping:
                    // ACK is sent automatically below; nothing else needed
                    break;

                case ENetProtocolCommand.SendReliable:
                    if (peer != null)
                        HandleSendReliable(peer, ref command, remaining);
                    break;

                case ENetProtocolCommand.SendUnreliable:
                    if (peer != null)
                        HandleSendUnreliable(peer, ref command, remaining);
                    break;

                case ENetProtocolCommand.SendUnsequenced:
                    if (peer != null)
                        HandleSendUnsequenced(peer, ref command, remaining);
                    break;

                case ENetProtocolCommand.SendFragment:
                case ENetProtocolCommand.SendUnreliableFragment:
                    if (peer != null)
                        HandleSendFragment(peer, ref command, remaining);
                    break;

                case ENetProtocolCommand.BandwidthLimit:
                    if (peer != null)
                        HandleBandwidthLimit(peer, ref command);
                    break;

                case ENetProtocolCommand.ThrottleConfigure:
                    if (peer != null)
                        HandleThrottleConfigure(peer, ref command);
                    break;
            }

            // Queue ACK if required
            if (peer != null && needsAck)
            {
                peer.QueueAcknowledgement(ref command, sentTime);
            }

            return 0;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Protocol command handlers
        // ─────────────────────────────────────────────────────────────────────

        private void HandleAcknowledge(ENetPeer peer, ref ENetProtocol command)
        {
            if (peer.State == ENetPeerState.Disconnected || peer.State == ENetPeerState.Zombie)
                return;

            ushort ackSeq = command.Acknowledge.ReceivedReliableSequenceNumber;
            ushort ackSentTime = command.Acknowledge.ReceivedSentTime;

            // Update RTT
            uint roundTripTime = (ushort)(ServiceTime - ackSentTime);
            if (roundTripTime > 0)
            {
                peer.LastRoundTripTime = roundTripTime;
                int rttDiff = (int)roundTripTime - (int)peer.RoundTripTime;
                peer.RoundTripTimeVariance = (uint)((int)peer.RoundTripTimeVariance * 3 / 4 + Math.Abs(rttDiff) / 4);
                peer.RoundTripTime = (uint)((int)peer.RoundTripTime * 7 / 8 + (int)roundTripTime / 8);
                if (peer.RoundTripTime < peer.LowestRoundTripTime)
                    peer.LowestRoundTripTime = peer.RoundTripTime;
                if (peer.RoundTripTimeVariance > peer.HighestRoundTripTimeVariance)
                    peer.HighestRoundTripTimeVariance = peer.RoundTripTimeVariance;
            }

            peer.LastReceiveTime = ServiceTime;
            peer.EarliestTimeout = 0;

            RemoveSentReliableCommand(peer, ackSeq, command.Acknowledge.Header.ChannelId);

            switch (peer.State)
            {
                case ENetPeerState.AcknowledgingConnect:
                    // Handled via VerifyConnect flow
                    break;
                case ENetPeerState.Disconnecting:
                    if ((ENetProtocolCommand)(command.Acknowledge.Header.Command & (byte)ENetProtocolCommand.Mask) ==
                        ENetProtocolCommand.Disconnect)
                        DispatchState(peer, ENetPeerState.Zombie);
                    break;
                case ENetPeerState.AcknowledgingDisconnect:
                    if ((ENetProtocolCommand)(command.Acknowledge.Header.Command & (byte)ENetProtocolCommand.Mask) ==
                        ENetProtocolCommand.Disconnect)
                        peer.Reset();
                    break;
                case ENetPeerState.DisconnectLater:
                    if (peer.OutgoingCommands.IsEmpty && peer.SentReliableCommands.IsEmpty)
                        peer.Disconnect(peer.EventData);
                    break;
            }
        }

        private ENetPeer? HandleConnect(ref ENetProtocol command)
        {
            if (_receivedAddress == null) return null;

            uint channelCount = command.Connect.ChannelCount;
            if (channelCount < ProtocolConstants.MinimumChannelCount ||
                channelCount > ProtocolConstants.MaximumChannelCount)
                return null;

            ENetPeer? peer = null;
            int duplicatePeers = 0;

            foreach (var p in Peers)
            {
                if (p.State == ENetPeerState.Disconnected)
                {
                    peer ??= p;
                }
                else if (p.State != ENetPeerState.Connecting &&
                         p.Address != null &&
                         AddressesEqual(p.Address, _receivedAddress))
                {
                    if (p.Address.Port == _receivedAddress.Port &&
                        p.ConnectId == command.Connect.ConnectId)
                        return null;
                    duplicatePeers++;
                }
            }

            if (peer == null || duplicatePeers >= DuplicatePeers)
                return null;

            if (channelCount > (uint)ChannelLimit)
                channelCount = (uint)ChannelLimit;

            if (peer.Channels == null || peer.Channels.Length < (int)channelCount)
            {
                peer.Channels = new ENetChannel[(int)channelCount];
                for (int i = 0; i < (int)channelCount; i++)
                    peer.Channels[i] = new ENetChannel();
            }
            else
            {
                for (int i = 0; i < (int)channelCount; i++)
                    peer.Channels[i].Reset();
            }
            peer.ChannelCount = (int)channelCount;
            peer.ConnectId = command.Connect.ConnectId;
            peer.Address = _receivedAddress;
            peer.OutgoingPeerId = command.Connect.OutgoingPeerId;
            peer.IncomingBandwidth = command.Connect.IncomingBandwidth;
            peer.OutgoingBandwidth = command.Connect.OutgoingBandwidth;
            peer.PacketThrottleInterval = command.Connect.PacketThrottleInterval;
            peer.PacketThrottleAcceleration = command.Connect.PacketThrottleAcceleration;
            peer.PacketThrottleDeceleration = command.Connect.PacketThrottleDeceleration;
            peer.EventData = command.Connect.Data;

            byte incomingSessionId = command.Connect.IncomingSessionId == 0xFF
                ? peer.OutgoingSessionId
                : command.Connect.IncomingSessionId;
            incomingSessionId = (byte)((incomingSessionId + 1) & 3);
            if (incomingSessionId == peer.OutgoingSessionId)
                incomingSessionId = (byte)((incomingSessionId + 1) & 3);
            peer.OutgoingSessionId = incomingSessionId;

            byte outgoingSessionId = command.Connect.OutgoingSessionId == 0xFF
                ? peer.IncomingSessionId
                : command.Connect.OutgoingSessionId;
            outgoingSessionId = (byte)((outgoingSessionId + 1) & 3);
            if (outgoingSessionId == peer.IncomingSessionId)
                outgoingSessionId = (byte)((outgoingSessionId + 1) & 3);
            peer.IncomingSessionId = outgoingSessionId;

            uint mtu = Math.Max((uint)ProtocolConstants.MinimumMtu,
                        Math.Min((uint)ProtocolConstants.MaximumMtu,
                                 command.Connect.Mtu));
            uint windowSize = Math.Max((uint)ProtocolConstants.MinimumWindowSize,
                              Math.Min((uint)ProtocolConstants.MaximumWindowSize,
                                       command.Connect.WindowSize));
            peer.Mtu = mtu;
            peer.WindowSize = windowSize;

            // Send VerifyConnect
            var verifyCmd = new ENetProtocol();
            verifyCmd.Header.Command = (byte)ENetProtocolCommand.VerifyConnect |
                                       (byte)ENetProtocolFlag.CommandFlagAcknowledge;
            verifyCmd.Header.ChannelId = 0xFF;
            verifyCmd.VerifyConnect.OutgoingPeerId = peer.IncomingPeerId;
            verifyCmd.VerifyConnect.IncomingSessionId = peer.IncomingSessionId;
            verifyCmd.VerifyConnect.OutgoingSessionId = peer.OutgoingSessionId;
            verifyCmd.VerifyConnect.Mtu = peer.Mtu;
            verifyCmd.VerifyConnect.WindowSize = peer.WindowSize;
            verifyCmd.VerifyConnect.ChannelCount = channelCount;
            verifyCmd.VerifyConnect.IncomingBandwidth = IncomingBandwidth;
            verifyCmd.VerifyConnect.OutgoingBandwidth = OutgoingBandwidth;
            verifyCmd.VerifyConnect.PacketThrottleInterval = peer.PacketThrottleInterval;
            verifyCmd.VerifyConnect.PacketThrottleAcceleration = peer.PacketThrottleAcceleration;
            verifyCmd.VerifyConnect.PacketThrottleDeceleration = peer.PacketThrottleDeceleration;
            verifyCmd.VerifyConnect.ConnectId = peer.ConnectId;

            peer.QueueOutgoingCommand(ref verifyCmd, null, 0, 0);
            return peer;
        }

        private int HandleVerifyConnect(ENetEvent? evt, ENetPeer peer, ref ENetProtocol command)
        {
            if (peer.State != ENetPeerState.Connecting) return 0;

            uint channelCount = command.VerifyConnect.ChannelCount;
            if (channelCount < ProtocolConstants.MinimumChannelCount ||
                channelCount > ProtocolConstants.MaximumChannelCount ||
                channelCount > (uint)ChannelLimit ||
                command.VerifyConnect.IncomingBandwidth != IncomingBandwidth ||
                command.VerifyConnect.OutgoingBandwidth != OutgoingBandwidth)
            {
                NotifyDisconnect(peer, evt);
                return 0;
            }

            RemoveSentReliableCommand(peer, command.Header.ReliableSequenceNumber, 0xFF);

            if (channelCount < (uint)peer.ChannelCount)
            {
                if (peer.Channels == null || peer.Channels.Length < (int)channelCount)
                {
                    peer.Channels = new ENetChannel[(int)channelCount];
                    for (int i = 0; i < (int)channelCount; i++)
                        peer.Channels[i] = new ENetChannel();
                }
                else
                {
                    for (int i = 0; i < (int)channelCount; i++)
                        peer.Channels[i].Reset();
                }
                peer.ChannelCount = (int)channelCount;
            }

            peer.OutgoingPeerId = command.VerifyConnect.OutgoingPeerId;
            peer.IncomingSessionId = command.VerifyConnect.IncomingSessionId;
            peer.OutgoingSessionId = command.VerifyConnect.OutgoingSessionId;

            uint mtu = Math.Max((uint)ProtocolConstants.MinimumMtu,
                        Math.Min((uint)ProtocolConstants.MaximumMtu,
                                 command.VerifyConnect.Mtu));
            uint windowSize = Math.Max((uint)ProtocolConstants.MinimumWindowSize,
                              Math.Min((uint)ProtocolConstants.MaximumWindowSize,
                                       command.VerifyConnect.WindowSize));
            peer.Mtu = mtu;
            peer.WindowSize = windowSize;

            NotifyConnect(peer, evt);
            return evt != null && evt.Type != ENetEventType.None ? 1 : 0;
        }

        private int HandleDisconnect(ENetEvent? evt, ENetPeer peer, ref ENetProtocol command)
        {
            if (peer.State == ENetPeerState.Disconnected ||
                peer.State == ENetPeerState.Zombie ||
                peer.State == ENetPeerState.AcknowledgingDisconnect)
                return 0;

            peer.ResetQueues();

            bool timeout = (ENetProtocolCommand)(command.Header.Command & (byte)ENetProtocolCommand.Mask) ==
                           ENetProtocolCommand.Disconnect &&
                           (command.Header.Command & (byte)ENetProtocolFlag.CommandFlagAcknowledge) == 0;

            if (peer.State == ENetPeerState.ConnectionSucceeded ||
                peer.State == ENetPeerState.Disconnecting ||
                peer.State == ENetPeerState.Connecting)
            {
                DispatchState(peer, ENetPeerState.Zombie);
            }
            else if (peer.State != ENetPeerState.Connected && peer.State != ENetPeerState.DisconnectLater)
            {
                peer.Reset();
            }
            else if ((command.Header.Command & (byte)ENetProtocolFlag.CommandFlagAcknowledge) != 0)
            {
                ChangeState(peer, ENetPeerState.AcknowledgingDisconnect);
            }
            else
            {
                DispatchState(peer, ENetPeerState.Zombie);
            }

            if (peer.State != ENetPeerState.Disconnected)
                peer.EventData = command.Disconnect.Data;

            return 0;
        }

        private void HandleSendReliable(ENetPeer peer, ref ENetProtocol command, ReadOnlySpan<byte> payloadData)
        {
            int dataLen = command.SendReliable.DataLength;
            if (dataLen > payloadData.Length) return;

            byte[] data = payloadData.Slice(0, dataLen).ToArray();
            peer.QueueIncomingCommand(ref command, data, dataLen,
                ENetPacketFlag.Reliable, 0);
        }

        private void HandleSendUnreliable(ENetPeer peer, ref ENetProtocol command, ReadOnlySpan<byte> payloadData)
        {
            int dataLen = command.SendUnreliable.DataLength;
            if (dataLen > payloadData.Length) return;

            byte[] data = payloadData.Slice(0, dataLen).ToArray();
            peer.QueueIncomingCommand(ref command, data, dataLen, ENetPacketFlag.None, 0);
        }

        private void HandleSendUnsequenced(ENetPeer peer, ref ENetProtocol command, ReadOnlySpan<byte> payloadData)
        {
            int dataLen = command.SendUnsequenced.DataLength;
            if (dataLen > payloadData.Length) return;

            uint group = command.SendUnsequenced.UnsequencedGroup;
            uint index = group % ProtocolConstants.PeerUnsequencedWindowSize;

            if (group < peer.IncomingUnsequencedGroup)
                group += 0x10000;

            if (group >= (uint)peer.IncomingUnsequencedGroup + ProtocolConstants.PeerFreeUnsequencedWindows * ProtocolConstants.PeerUnsequencedWindowSize)
                return;

            group %= ProtocolConstants.PeerUnsequencedWindowSize;
            if (group / 32 >= (uint)(ProtocolConstants.PeerUnsequencedWindowSize / 32))
                return;

            if ((peer.UnsequencedWindow[index / 32] & (1u << ((int)(index % 32)))) != 0)
                return;

            byte[] data = payloadData.Slice(0, dataLen).ToArray();
            if (peer.QueueIncomingCommand(ref command, data, dataLen, ENetPacketFlag.Unsequenced, 0) == null)
                return;

            peer.UnsequencedWindow[index / 32] |= 1u << ((int)(index % 32));
        }

        private void HandleSendFragment(ENetPeer peer, ref ENetProtocol command, ReadOnlySpan<byte> payloadData)
        {
            int dataLen = command.SendFragment.DataLength;
            if (dataLen > payloadData.Length) return;

            byte channelId = command.Header.ChannelId;
            if (channelId >= peer.ChannelCount || peer.Channels == null) return;

            var channel = peer.Channels[channelId];
            ushort startSeq = command.SendFragment.StartSequenceNumber;
            ushort fragSeq = (ushort)(startSeq % ProtocolConstants.PeerReliableWindowSize);
            ushort curSeq = (ushort)(channel.IncomingReliableSequenceNumber % ProtocolConstants.PeerReliableWindowSize);

            if (startSeq < channel.IncomingReliableSequenceNumber)
                startSeq += (ushort)ProtocolConstants.PeerReliableWindows;

            if (startSeq >= (ushort)(channel.IncomingReliableSequenceNumber + ProtocolConstants.PeerFreeReliableWindows))
                return;

            uint fragmentNumber = command.SendFragment.FragmentNumber;
            uint fragmentCount = command.SendFragment.FragmentCount;
            uint fragmentOffset = command.SendFragment.FragmentOffset;
            uint totalLength = command.SendFragment.TotalLength;

            if (fragmentCount > ProtocolConstants.MaximumFragmentCount ||
                fragmentNumber >= fragmentCount ||
                totalLength > MaximumPacketSize ||
                fragmentOffset >= totalLength ||
                dataLen > totalLength - fragmentOffset)
                return;

            // Look for existing incomplete incoming command
            ENetIncomingCommand? startCommand = null;
            for (var node = channel.IncomingReliableCommands.Begin;
                 node != channel.IncomingReliableCommands.End;
                 node = node.Next!)
            {
                var cmd = node.Owner!;
                if (startSeq >= channel.IncomingReliableSequenceNumber)
                {
                    if (cmd.ReliableSequenceNumber < channel.IncomingReliableSequenceNumber)
                        continue;
                }
                else if (cmd.ReliableSequenceNumber >= channel.IncomingReliableSequenceNumber)
                    break;

                if (cmd.ReliableSequenceNumber <= startSeq)
                {
                    if (cmd.ReliableSequenceNumber < startSeq) break;
                    if (cmd.FragmentCount != fragmentCount ||
                        cmd.Packet == null ||
                        cmd.Packet.DataLength != totalLength)
                        return;
                    startCommand = cmd;
                    break;
                }
            }

            if (startCommand == null)
            {
                var proto = command;
                proto.Header.ReliableSequenceNumber = startSeq;
                byte[] buf = new byte[totalLength];
                startCommand = peer.QueueIncomingCommand(ref proto, buf, (int)totalLength,
                    ENetPacketFlag.Reliable, fragmentCount);
                if (startCommand == null) return;
            }

            if (startCommand.Fragments != null &&
                fragmentNumber < fragmentCount)
            {
                uint bitIndex = fragmentNumber / 32;
                uint bitMask = 1u << (int)(fragmentNumber % 32);
                if ((startCommand.Fragments[bitIndex] & bitMask) == 0)
                {
                    startCommand.FragmentsRemaining--;
                    startCommand.Fragments[bitIndex] |= bitMask;

                    if (fragmentOffset + dataLen > totalLength)
                        dataLen = (int)(totalLength - fragmentOffset);

                    if (startCommand.Packet != null)
                        payloadData.Slice(0, dataLen).CopyTo(
                            new Span<byte>(startCommand.Packet.Data, (int)fragmentOffset, dataLen));
                }
            }

            if (startCommand.FragmentsRemaining == 0)
            {
                peer.DispatchIncomingReliableCommands(channel, startCommand);
            }
        }

        private void HandleBandwidthLimit(ENetPeer peer, ref ENetProtocol command)
        {
            peer.IncomingBandwidth = command.BandwidthLimit.IncomingBandwidth;
            peer.OutgoingBandwidth = command.BandwidthLimit.OutgoingBandwidth;

            if (peer.IncomingBandwidth == 0 && OutgoingBandwidth == 0)
                peer.WindowSize = ProtocolConstants.MaximumWindowSize;
            else
            {
                peer.WindowSize = (uint)(Math.Min(peer.IncomingBandwidth, OutgoingBandwidth) /
                    ProtocolConstants.PeerWindowSizeScale * ProtocolConstants.PeerDefaultRoundTripTime);
            }

            peer.WindowSize = (uint)Math.Max(ProtocolConstants.MinimumWindowSize,
                              Math.Min(ProtocolConstants.MaximumWindowSize, (int)peer.WindowSize));
        }

        private void HandleThrottleConfigure(ENetPeer peer, ref ENetProtocol command)
        {
            peer.PacketThrottleInterval = command.ThrottleConfigure.PacketThrottleInterval;
            peer.PacketThrottleAcceleration = command.ThrottleConfigure.PacketThrottleAcceleration;
            peer.PacketThrottleDeceleration = command.ThrottleConfigure.PacketThrottleDeceleration;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Send outgoing commands
        // ─────────────────────────────────────────────────────────────────────

        private void SendOutgoingCommands(ENetEvent? evt, bool checkForTimeouts)
        {
            // Use a scratch buffer for assembling UDP datagrams
            var sendBuffer = new byte[ProtocolConstants.MaximumMtu];

            foreach (var peer in Peers)
            {
                if (peer.State == ENetPeerState.Disconnected ||
                    peer.State == ENetPeerState.Zombie)
                    continue;

                peer.PacketThrottleCounter += ProtocolConstants.PeerPacketThrottleCounter;
                peer.PacketThrottleCounter %= ProtocolConstants.PeerPacketThrottleScale;

                SendAcknowledgements(peer, sendBuffer);

                if (checkForTimeouts && evt != null)
                {
                    if (CheckTimeouts(peer, evt) == 1)
                    {
                        if (evt.Type != ENetEventType.None) return;
                    }
                }

                if ((peer.OutgoingCommands.IsEmpty && peer.SentReliableCommands.IsEmpty) ||
                    ContinueSendingReliable(peer, sendBuffer))
                {
                    // Send any unreliable commands
                    SendUnreliableCommands(peer, sendBuffer);
                }

                // Check ping interval
                if (peer.State == ENetPeerState.Connected &&
                    TimeUtils.TimeGreaterEqual(ServiceTime, peer.LastReceiveTime + peer.PingInterval) &&
                    peer.Mtu - GetSentDataSize(peer) >= CommandSizes.GetCommandSize((byte)ENetProtocolCommand.Ping))
                {
                    peer.Ping();
                    SendAcknowledgements(peer, sendBuffer);
                }

                if (!peer.SentReliableCommands.IsEmpty)
                {
                    var first = (ENetOutgoingCommand)peer.SentReliableCommands.Front!;
                    peer.NextTimeout = first.SentTime + first.RoundTripTimeout;
                }
            }
        }

        private int GetSentDataSize(ENetPeer peer) => 0; // simplified

        private void SendAcknowledgements(ENetPeer peer, byte[] sendBuffer)
        {
            if (peer.Acknowledgements.IsEmpty || peer.Address == null) return;

            int offset = CommandSizes.ProtocolHeaderSize; // reserve header space

            var node = peer.Acknowledgements.Begin;
            while (node != peer.Acknowledgements.End && offset + 8 <= sendBuffer.Length)
            {
                var ack = node.Owner!;
                var next = node.Next!;
                peer.Acknowledgements.Remove(node);
                node = next;

                // Write ACK command
                var ackCmd = new ProtocolAcknowledge
                {
                    Header = new ProtocolCommandHeader
                    {
                        Command = (byte)ENetProtocolCommand.Acknowledge,
                        ChannelId = ack.Command.Header.ChannelId,
                        ReliableSequenceNumber = ack.Command.Header.ReliableSequenceNumber
                    },
                    ReceivedReliableSequenceNumber = ack.Command.Header.ReliableSequenceNumber,
                    ReceivedSentTime = (ushort)ack.SentTime
                };

                ackCmd.Write(sendBuffer.AsSpan(offset));
                offset += 8;
                Pool.ReturnAck(ack);
            }

            FlushBuffer(peer, sendBuffer, offset);
        }

        private bool ContinueSendingReliable(ENetPeer peer, byte[] sendBuffer)
        {
            if (peer.OutgoingCommands.IsEmpty) return false;

            int offset = CommandSizes.ProtocolHeaderSize;

            var node = peer.OutgoingCommands.Begin;
            while (node != peer.OutgoingCommands.End)
            {
                var cmd = node.Owner!;
                var next = node.Next!;

                bool isReliable = (cmd.Command.Header.Command & (byte)ENetProtocolFlag.CommandFlagAcknowledge) != 0;

                if (!isReliable)
                {
                    node = next;
                    continue;
                }

                int cmdSize = CommandSizes.GetCommandSize(cmd.Command.Header.Command);
                int dataLen = cmd.Packet != null ? cmd.FragmentLength : 0;

                if (offset + cmdSize + dataLen > sendBuffer.Length)
                    break;

                // Check window
                var channel = cmd.Command.Header.ChannelId < peer.ChannelCount && peer.Channels != null
                    ? peer.Channels[cmd.Command.Header.ChannelId]
                    : null;

                if (channel != null)
                {
                    ushort reliableWindow = (ushort)(cmd.ReliableSequenceNumber / ProtocolConstants.PeerReliableWindowSize);
                    ushort currentWindow = (ushort)(channel.IncomingReliableSequenceNumber / ProtocolConstants.PeerReliableWindowSize);

                    if (cmd.ReliableSequenceNumber < channel.IncomingReliableSequenceNumber)
                        reliableWindow += (ushort)ProtocolConstants.PeerReliableWindows;

                    if (reliableWindow >= currentWindow + ProtocolConstants.PeerFreeReliableWindows)
                        break;

                    if ((ushort)(channel.UsedReliableWindows & (1 << (reliableWindow % ProtocolConstants.PeerReliableWindows))) != 0 &&
                        channel.ReliableWindows[reliableWindow % ProtocolConstants.PeerReliableWindows] >= ProtocolConstants.PeerReliableWindowSize)
                        break;
                }

                // Move from outgoing → sentReliable
                peer.OutgoingCommands.Remove(cmd.ListNode);
                peer.SentReliableCommands.Insert(peer.SentReliableCommands.End, cmd.ListNode);

                cmd.SentTime = ServiceTime;
                cmd.RoundTripTimeout = (ushort)Math.Max(1, peer.RoundTripTime + 4 * peer.RoundTripTimeVariance);
                cmd.RoundTripTimeoutLimit = peer.TimeoutLimit * cmd.RoundTripTimeout;
                cmd.SendAttempts++;
                peer.ReliableDataInTransit += cmd.FragmentLength;
                peer.TotalDataSent += cmd.FragmentLength;
                peer.TotalPacketsSent++;

                // Track window usage
                if (channel != null)
                {
                    ushort reliableWindow = (ushort)((cmd.ReliableSequenceNumber / ProtocolConstants.PeerReliableWindowSize) % ProtocolConstants.PeerReliableWindows);
                    channel.UsedReliableWindows |= (ushort)(1 << reliableWindow);
                    channel.ReliableWindows[reliableWindow]++;
                }

                // Write command to buffer
                WriteCommand(ref cmd.Command, sendBuffer.AsSpan(offset));
                offset += cmdSize;

                // Write payload
                if (cmd.Packet != null && dataLen > 0)
                {
                    Buffer.BlockCopy(cmd.Packet.Data, (int)cmd.FragmentOffset, sendBuffer, offset, dataLen);
                    offset += dataLen;
                }

                node = next;
            }

            if (offset > CommandSizes.ProtocolHeaderSize)
            {
                FlushBuffer(peer, sendBuffer, offset);
                return true;
            }

            return false;
        }

        private void SendUnreliableCommands(ENetPeer peer, byte[] sendBuffer)
        {
            if (peer.OutgoingCommands.IsEmpty) return;

            int offset = CommandSizes.ProtocolHeaderSize;

            var node = peer.OutgoingCommands.Begin;
            while (node != peer.OutgoingCommands.End)
            {
                var cmd = node.Owner!;
                var next = node.Next!;

                bool isReliable = (cmd.Command.Header.Command & (byte)ENetProtocolFlag.CommandFlagAcknowledge) != 0;
                if (isReliable)
                {
                    node = next;
                    continue;
                }

                int cmdSize = CommandSizes.GetCommandSize(cmd.Command.Header.Command);
                int dataLen = cmd.Packet != null ? cmd.FragmentLength : 0;

                if (offset + cmdSize + dataLen > sendBuffer.Length)
                    break;

                peer.OutgoingCommands.Remove(cmd.ListNode);

                if (cmd.Packet != null)
                {
                    // Move to sentUnreliable briefly for cleanup
                    peer.SentUnreliableCommands.Insert(peer.SentUnreliableCommands.End, cmd.ListNode);
                }

                cmd.SendAttempts++;
                peer.TotalDataSent += cmd.FragmentLength;
                peer.TotalPacketsSent++;

                WriteCommand(ref cmd.Command, sendBuffer.AsSpan(offset));
                offset += cmdSize;

                if (cmd.Packet != null && dataLen > 0)
                {
                    Buffer.BlockCopy(cmd.Packet.Data, (int)cmd.FragmentOffset, sendBuffer, offset, dataLen);
                    offset += dataLen;
                }

                node = next;
            }

            if (offset > CommandSizes.ProtocolHeaderSize)
                FlushBuffer(peer, sendBuffer, offset);

            RemoveSentUnreliableCommands(peer);
        }

        private void RemoveSentUnreliableCommands(ENetPeer peer)
        {
            while (!peer.SentUnreliableCommands.IsEmpty)
            {
                var cmd = peer.SentUnreliableCommands.Front!;
                peer.SentUnreliableCommands.Remove(cmd.ListNode);

                if (cmd.Packet != null)
                {
                    cmd.Packet.ReferenceCount--;
                    if (cmd.Packet.ReferenceCount == 0)
                    {
                        cmd.Packet.Flags |= ENetPacketFlag.Sent;
                        cmd.Packet.Destroy();
                    }
                }

                Pool.ReturnOutgoing(cmd);
            }

            if (peer.State == ENetPeerState.DisconnectLater &&
                peer.OutgoingCommands.IsEmpty &&
                peer.SentReliableCommands.IsEmpty)
                peer.Disconnect(peer.EventData);
        }

        private void FlushBuffer(ENetPeer peer, byte[] buffer, int length)
        {
            if (peer.Address == null || length <= CommandSizes.ProtocolHeaderSize) return;

            // Write protocol header (first 2 or 4 bytes)
            bool includeSentTime = true;
            ushort peerId = (ushort)(peer.OutgoingPeerId |
                ((peer.OutgoingSessionId & 3) << (int)ENetProtocolFlag.HeaderSessionShift));

            if (includeSentTime)
            {
                peerId |= (ushort)ENetProtocolFlag.HeaderFlagSentTime;
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(0), peerId);
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(2), (ushort)ServiceTime);
            }
            else
            {
                System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(0), peerId);
            }

            _socket.SendTo(buffer, 0, length, peer.Address);
            TotalSentData += (ulong)length;
            TotalSentPackets++;
            peer.LastSendTime = ServiceTime;
        }

        private void WriteCommand(ref ENetProtocol cmd, Span<byte> buf)
        {
            var cmdType = (ENetProtocolCommand)(cmd.Header.Command & (byte)ENetProtocolCommand.Mask);
            switch (cmdType)
            {
                case ENetProtocolCommand.Acknowledge:
                    cmd.Acknowledge.Write(buf); break;
                case ENetProtocolCommand.Connect:
                    cmd.Connect.Write(buf); break;
                case ENetProtocolCommand.VerifyConnect:
                    cmd.VerifyConnect.Write(buf); break;
                case ENetProtocolCommand.Disconnect:
                    cmd.Disconnect.Write(buf); break;
                case ENetProtocolCommand.Ping:
                    cmd.Ping.Write(buf); break;
                case ENetProtocolCommand.SendReliable:
                    cmd.SendReliable.Write(buf); break;
                case ENetProtocolCommand.SendUnreliable:
                    cmd.SendUnreliable.Write(buf); break;
                case ENetProtocolCommand.SendUnsequenced:
                    cmd.SendUnsequenced.Write(buf); break;
                case ENetProtocolCommand.SendFragment:
                case ENetProtocolCommand.SendUnreliableFragment:
                    cmd.SendFragment.Write(buf); break;
                case ENetProtocolCommand.BandwidthLimit:
                    cmd.BandwidthLimit.Write(buf); break;
                case ENetProtocolCommand.ThrottleConfigure:
                    cmd.ThrottleConfigure.Write(buf); break;
                default:
                    cmd.Header.Write(buf); break;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Timeout handling
        // ─────────────────────────────────────────────────────────────────────

        private int CheckTimeouts(ENetPeer peer, ENetEvent? evt)
        {
            if (peer.SentReliableCommands.IsEmpty) return 0;

            var node = peer.SentReliableCommands.Begin;
            while (node != peer.SentReliableCommands.End)
            {
                var cmd = node.Owner!;
                var next = node.Next!;
                node = next;

                if (TimeUtils.TimeLess(ServiceTime, cmd.SentTime + cmd.RoundTripTimeout))
                    continue;

                if (peer.EarliestTimeout == 0 ||
                    TimeUtils.TimeLess(cmd.SentTime, peer.EarliestTimeout))
                    peer.EarliestTimeout = cmd.SentTime;

                if (peer.EarliestTimeout != 0 &&
                    (TimeUtils.TimeGreaterEqual(ServiceTime, peer.EarliestTimeout + peer.TimeoutMaximum) ||
                     (cmd.RoundTripTimeout >= cmd.RoundTripTimeoutLimit &&
                      TimeUtils.TimeGreaterEqual(ServiceTime, peer.EarliestTimeout + peer.TimeoutMinimum))))
                {
                    NotifyDisconnectTimeout(peer, evt);
                    return evt?.Type != ENetEventType.None ? 1 : 0;
                }

                // Retransmit: move back to outgoing queue
                if (cmd.Packet != null)
                    peer.ReliableDataInTransit -= cmd.FragmentLength;

                peer.SentReliableCommands.Remove(cmd.ListNode);

                cmd.SendAttempts++;
                cmd.RoundTripTimeout *= 2;
                if (cmd.RoundTripTimeout > cmd.RoundTripTimeoutLimit)
                    cmd.RoundTripTimeout = cmd.RoundTripTimeoutLimit;

                peer.OutgoingCommands.Insert(peer.OutgoingCommands.Begin, cmd.ListNode);
            }

            return 0;
        }

        private void NotifyConnect(ENetPeer peer, ENetEvent? evt)
        {
            RecalculateBandwidthLimits = true;
            if (evt != null)
            {
                ChangeState(peer, ENetPeerState.Connected);
                peer.TotalDataSent = 0;
                peer.TotalDataReceived = 0;
                peer.TotalPacketsSent = 0;
                peer.TotalPacketsLost = 0;
                evt.Type = ENetEventType.Connect;
                evt.Peer = peer;
                evt.Data = peer.EventData;
            }
            else
            {
                DispatchState(peer, peer.State == ENetPeerState.Connecting
                    ? ENetPeerState.ConnectionSucceeded
                    : ENetPeerState.ConnectionPending);
            }
        }

        private void NotifyDisconnect(ENetPeer peer, ENetEvent? evt)
        {
            if (peer.State >= ENetPeerState.ConnectionPending)
                RecalculateBandwidthLimits = true;

            if (peer.State != ENetPeerState.Connecting &&
                peer.State < ENetPeerState.ConnectionSucceeded)
            {
                peer.Reset();
            }
            else if (evt != null)
            {
                evt.Type = ENetEventType.Disconnect;
                evt.Peer = peer;
                evt.Data = 0;
                peer.Reset();
            }
            else
            {
                peer.EventData = 0;
                DispatchState(peer, ENetPeerState.Zombie);
            }
        }

        private void NotifyDisconnectTimeout(ENetPeer peer, ENetEvent? evt)
        {
            if (peer.State >= ENetPeerState.ConnectionPending)
                RecalculateBandwidthLimits = true;

            if (peer.State != ENetPeerState.Connecting &&
                peer.State < ENetPeerState.ConnectionSucceeded)
            {
                peer.Reset();
            }
            else if (evt != null)
            {
                evt.Type = ENetEventType.DisconnectTimeout;
                evt.Peer = peer;
                evt.Data = 0;
                peer.Reset();
            }
            else
            {
                peer.EventData = 0;
                DispatchState(peer, ENetPeerState.Zombie);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Bandwidth throttling
        // ─────────────────────────────────────────────────────────────────────

        private void BandwidthThrottle()
        {
            BandwidthThrottleEpoch = ServiceTime;

            if (ConnectedPeers == 0) return;

            uint elapsedTime = ServiceTime - BandwidthThrottleEpoch;
            uint peersRemaining = (uint)ConnectedPeers;
            uint dataTotal = uint.MaxValue;

            bool throttle = false;

            if (OutgoingBandwidth == 0 && IncomingBandwidth == 0)
                return;

            if (OutgoingBandwidth != 0)
            {
                uint outgoingBandwidth = OutgoingBandwidth / 1000 * (elapsedTime > 0 ? elapsedTime : 1);
                dataTotal = outgoingBandwidth;
                throttle = true;
            }

            foreach (var peer in Peers)
            {
                if (peer.State != ENetPeerState.Connected &&
                    peer.State != ENetPeerState.DisconnectLater)
                    continue;

                if (throttle)
                {
                    // Simplified: set window size proportional to bandwidth
                    uint windowSize = dataTotal / peersRemaining;
                    peer.WindowSize = (uint)Math.Max(ProtocolConstants.MinimumWindowSize,
                                      Math.Min(ProtocolConstants.MaximumWindowSize, (int)windowSize));
                }

                if (RecalculateBandwidthLimits)
                {
                    SendBandwidthLimit(peer);
                }
            }

            RecalculateBandwidthLimits = false;
        }

        private void SendBandwidthLimit(ENetPeer peer)
        {
            var cmd = new ENetProtocol();
            cmd.Header.Command = (byte)ENetProtocolCommand.BandwidthLimit |
                                  (byte)ENetProtocolFlag.CommandFlagAcknowledge;
            cmd.Header.ChannelId = 0xFF;
            cmd.BandwidthLimit.IncomingBandwidth = IncomingBandwidth;
            cmd.BandwidthLimit.OutgoingBandwidth = OutgoingBandwidth;
            peer.QueueOutgoingCommand(ref cmd, null, 0, 0);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private void RemoveSentReliableCommand(ENetPeer peer, ushort reliableSeq, byte channelId)
        {
            ENetOutgoingCommand? found = null;
            var node = peer.SentReliableCommands.Begin;
            while (node != peer.SentReliableCommands.End)
            {
                var cmd = node.Owner!;
                if (cmd.ReliableSequenceNumber == reliableSeq &&
                    cmd.Command.Header.ChannelId == channelId)
                {
                    found = cmd;
                    break;
                }
                node = node.Next!;
            }

            if (found == null)
            {
                // Check outgoing queue too
                node = peer.OutgoingCommands.Begin;
                while (node != peer.OutgoingCommands.End)
                {
                    var cmd = node.Owner!;
                    if (cmd.SendAttempts < 1) return;
                    if (cmd.ReliableSequenceNumber == reliableSeq &&
                        cmd.Command.Header.ChannelId == channelId)
                    {
                        found = cmd;
                        break;
                    }
                    node = node.Next!;
                }
                if (found == null) return;
                peer.OutgoingCommands.Remove(found.ListNode);
            }
            else
            {
                peer.SentReliableCommands.Remove(found.ListNode);
            }

            // Update channel window
            if (channelId < peer.ChannelCount && peer.Channels != null)
            {
                var channel = peer.Channels[channelId];
                ushort reliableWindow = (ushort)((reliableSeq / ProtocolConstants.PeerReliableWindowSize) % ProtocolConstants.PeerReliableWindows);
                if (channel.ReliableWindows[reliableWindow] > 0)
                {
                    channel.ReliableWindows[reliableWindow]--;
                    if (channel.ReliableWindows[reliableWindow] == 0)
                        channel.UsedReliableWindows &= (ushort)~(1 << reliableWindow);
                }
            }

            // Release packet
            if (found.Packet != null)
            {
                if ((found.Command.Header.Command & (byte)ENetProtocolFlag.CommandFlagAcknowledge) != 0)
                    peer.ReliableDataInTransit -= found.FragmentLength;

                found.Packet.ReferenceCount--;
                if (found.Packet.ReferenceCount == 0)
                {
                    found.Packet.Flags |= ENetPacketFlag.Sent;
                    found.Packet.Destroy();
                }
            }

            Pool.ReturnOutgoing(found);

            if (!peer.SentReliableCommands.IsEmpty)
            {
                var first = peer.SentReliableCommands.Front!;
                peer.NextTimeout = first.SentTime + first.RoundTripTimeout;
            }
        }

        private static bool AddressesEqual(IPEndPoint a, IPEndPoint b)
        {
            if (a.Port != b.Port) return false;
            var addrA = a.Address.IsIPv4MappedToIPv6 ? a.Address.MapToIPv4() : a.Address;
            var addrB = b.Address.IsIPv4MappedToIPv6 ? b.Address.MapToIPv4() : b.Address;
            return addrA.Equals(addrB);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Stats accessors
        // ─────────────────────────────────────────────────────────────────────

        public uint PeersCount => (uint)ConnectedPeers;
        public uint PacketsSent => TotalSentPackets;
        public uint PacketsReceived => TotalReceivedPackets;
        public uint BytesSent => (uint)TotalSentData;
        public uint BytesReceived => TotalReceivedData;

        // ─────────────────────────────────────────────────────────────────────
        // IDisposable
        // ─────────────────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (!_disposed)
            {
                Destroy();
                _disposed = true;
            }
        }
    }
}
