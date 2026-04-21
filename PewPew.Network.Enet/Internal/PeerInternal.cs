using System;
using System.Net;

namespace PewPew.Network.Enet.Internal
{
    internal enum ENetPeerState
    {
        Disconnected = 0,
        Connecting = 1,
        AcknowledgingConnect = 2,
        ConnectionPending = 3,
        ConnectionSucceeded = 4,
        Connected = 5,
        DisconnectLater = 6,
        Disconnecting = 7,
        AcknowledgingDisconnect = 8,
        Zombie = 9
    }

    /// <summary>
    /// Internal peer representation. Matches _ENetPeer in the C code.
    /// </summary>
    internal class ENetPeer
    {
        // ── List membership ──────────────────────────────────────────────────
        public readonly ENetListNode<ENetPeer> DispatchListNode;

        // ── Identity ─────────────────────────────────────────────────────────
        public ENetHostInternal? Host;
        public ushort OutgoingPeerId;
        public ushort IncomingPeerId;
        public uint ConnectId;
        public byte OutgoingSessionId;
        public byte IncomingSessionId;

        // ── Network address ───────────────────────────────────────────────────
        public IPEndPoint? Address;

        // ── User data ─────────────────────────────────────────────────────────
        public object? UserData;

        // ── State ─────────────────────────────────────────────────────────────
        public ENetPeerState State;

        // ── Channels ──────────────────────────────────────────────────────────
        public ENetChannel[]? Channels;
        public int ChannelCount;

        // ── Bandwidth ─────────────────────────────────────────────────────────
        public uint IncomingBandwidth;
        public uint OutgoingBandwidth;
        public uint IncomingBandwidthThrottleEpoch;
        public uint OutgoingBandwidthThrottleEpoch;

        // ── Data totals ───────────────────────────────────────────────────────
        public uint IncomingDataTotal;
        public ulong TotalDataReceived;
        public uint OutgoingDataTotal;
        public ulong TotalDataSent;
        public uint LastSendTime;
        public uint LastReceiveTime;
        public uint NextTimeout;
        public uint EarliestTimeout;
        public ulong TotalPacketsSent;
        public ulong TotalPacketsLost;

        // ── Throttle ──────────────────────────────────────────────────────────
        public uint PacketThrottle;
        public uint PacketThrottleThreshold;
        public uint PacketThrottleLimit;
        public uint PacketThrottleCounter;
        public uint PacketThrottleEpoch;
        public uint PacketThrottleAcceleration;
        public uint PacketThrottleDeceleration;
        public uint PacketThrottleInterval;

        // ── Timing ────────────────────────────────────────────────────────────
        public uint PingInterval;
        public uint TimeoutLimit;
        public uint TimeoutMinimum;
        public uint TimeoutMaximum;
        public uint LastRoundTripTime;
        public uint LowestRoundTripTime;
        public uint LastRoundTripTimeVariance;
        public uint HighestRoundTripTimeVariance;
        public uint RoundTripTime;
        public uint RoundTripTimeVariance;
        public uint Mtu;
        public uint WindowSize;
        public uint ReliableDataInTransit;
        public ushort OutgoingReliableSequenceNumber;

        // ── Command queues ────────────────────────────────────────────────────
        public readonly ENetList<ENetAcknowledgement> Acknowledgements = new();
        public readonly ENetList<ENetOutgoingCommand> SentReliableCommands = new();
        public readonly ENetList<ENetOutgoingCommand> SentUnreliableCommands = new();
        public readonly ENetList<ENetOutgoingCommand> OutgoingCommands = new();
        public readonly ENetList<ENetIncomingCommand> DispatchedCommands = new();

        public bool NeedsDispatch;

        // ── Unsequenced ───────────────────────────────────────────────────────
        public ushort IncomingUnsequencedGroup;
        public ushort OutgoingUnsequencedGroup;
        public uint[] UnsequencedWindow = new uint[ProtocolConstants.PeerUnsequencedWindowSize / 32];

        public uint EventData;
        public int TotalWaitingData;

        public ENetPeer()
        {
            DispatchListNode = new ENetListNode<ENetPeer> { Owner = this };
            Reset();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API (mirrors enet_peer_* functions)
        // ─────────────────────────────────────────────────────────────────────

        public void Reset()
        {
            State = ENetPeerState.Disconnected;
            OutgoingPeerId = ProtocolConstants.MaximumPeerId;
            IncomingPeerId = 0;
            ConnectId = 0;
            OutgoingSessionId = 0xFF;
            IncomingSessionId = 0xFF;
            Address = null;
            UserData = null;
            Channels = null;
            ChannelCount = 0;
            IncomingBandwidth = 0;
            OutgoingBandwidth = 0;
            IncomingBandwidthThrottleEpoch = 0;
            OutgoingBandwidthThrottleEpoch = 0;
            IncomingDataTotal = 0;
            TotalDataReceived = 0;
            OutgoingDataTotal = 0;
            TotalDataSent = 0;
            LastSendTime = 0;
            LastReceiveTime = 0;
            NextTimeout = 0;
            EarliestTimeout = 0;
            TotalPacketsSent = 0;
            TotalPacketsLost = 0;
            PacketThrottle = ProtocolConstants.PeerDefaultPacketThrottle;
            PacketThrottleThreshold = ProtocolConstants.PeerPacketThrottleThreshold;
            PacketThrottleLimit = ProtocolConstants.PeerPacketThrottleScale;
            PacketThrottleCounter = 0;
            PacketThrottleEpoch = 0;
            PacketThrottleAcceleration = ProtocolConstants.PeerPacketThrottleAcceleration;
            PacketThrottleDeceleration = ProtocolConstants.PeerPacketThrottleDeceleration;
            PacketThrottleInterval = ProtocolConstants.PeerPacketThrottleInterval;
            PingInterval = ProtocolConstants.PeerPingInterval;
            TimeoutLimit = ProtocolConstants.PeerTimeoutLimit;
            TimeoutMinimum = ProtocolConstants.PeerTimeoutMinimum;
            TimeoutMaximum = ProtocolConstants.PeerTimeoutMaximum;
            RoundTripTime = ProtocolConstants.PeerDefaultRoundTripTime;
            RoundTripTimeVariance = 0;
            LowestRoundTripTime = ProtocolConstants.PeerDefaultRoundTripTime;
            LastRoundTripTime = ProtocolConstants.PeerDefaultRoundTripTime;
            LastRoundTripTimeVariance = 0;
            HighestRoundTripTimeVariance = 0;
            Mtu = 0;
            WindowSize = 0;
            ReliableDataInTransit = 0;
            OutgoingReliableSequenceNumber = 0;
            NeedsDispatch = false;
            IncomingUnsequencedGroup = 0;
            OutgoingUnsequencedGroup = 0;
            EventData = 0;
            TotalWaitingData = 0;

            Array.Clear(UnsequencedWindow, 0, UnsequencedWindow.Length);

            ResetQueues();
        }

        public void ResetQueues()
        {
            if (NeedsDispatch && Host != null)
            {
                Host.DispatchQueue.Remove(DispatchListNode);
                NeedsDispatch = false;
            }

            Acknowledgements.Clear();
            SentReliableCommands.Clear();
            SentUnreliableCommands.Clear();
            OutgoingCommands.Clear();
            DispatchedCommands.Clear();
        }

        public void OnConnect()
        {
            if (Host == null) return;
            if (State == ENetPeerState.Connected || State == ENetPeerState.DisconnectLater)
            {
                Host.ConnectedPeers++;
                if (OutgoingBandwidth == 0)
                    Host.BandwidthLimitedPeers++;
            }
        }

        public void OnDisconnect()
        {
            if (Host == null) return;
            if (State == ENetPeerState.Connected || State == ENetPeerState.DisconnectLater)
            {
                Host.ConnectedPeers--;
                if (OutgoingBandwidth == 0)
                    Host.BandwidthLimitedPeers--;
            }
        }

        /// <summary>
        /// Apply packet throttle. Returns 1 if throttled (dropped), 0 if allowed through.
        /// Mirrors enet_peer_throttle().
        /// </summary>
        public int Throttle(uint rtt)
        {
            if (LastRoundTripTime <= LastRoundTripTime + LastRoundTripTimeVariance)
            {
                PacketThrottle = PacketThrottleLimit;
                return 1;
            }

            if (rtt <= LastRoundTripTime)
            {
                PacketThrottle += PacketThrottleAcceleration;
                if (PacketThrottle > PacketThrottleLimit)
                    PacketThrottle = PacketThrottleLimit;
                return 1;
            }

            if (rtt > LastRoundTripTime + 2 * LastRoundTripTimeVariance)
            {
                if (PacketThrottle > PacketThrottleDeceleration)
                    PacketThrottle -= PacketThrottleDeceleration;
                else
                    PacketThrottle = 0;
                return 0;
            }

            return 1;
        }

        /// <summary>
        /// Setup an outgoing command's roundtrip-timeout fields and add it to the peer's
        /// outgoing or sent queue. Mirrors enet_peer_setup_outgoing_command().
        /// </summary>
        public void SetupOutgoingCommand(ENetOutgoingCommand outgoingCommand)
        {
            byte commandType = (byte)(outgoingCommand.Command.Header.Command & (byte)ENetProtocolCommand.Mask);
            bool isReliable = (outgoingCommand.Command.Header.Command & (byte)ENetProtocolFlag.CommandFlagAcknowledge) != 0;

            outgoingCommand.SendAttempts = 0;
            outgoingCommand.SentTime = 0;
            outgoingCommand.RoundTripTimeout = 0;
            outgoingCommand.RoundTripTimeoutLimit = 0;

            if (outgoingCommand.Packet != null)
                outgoingCommand.Packet.ReferenceCount++;

            OutgoingCommands.Insert(OutgoingCommands.End, outgoingCommand.ListNode);
        }

        /// <summary>
        /// Queue an outgoing command for a specific channel. Returns the new command or null.
        /// Mirrors enet_peer_queue_outgoing_command().
        /// </summary>
        public ENetOutgoingCommand? QueueOutgoingCommand(
            ref ENetProtocol command,
            ENetPacket? packet,
            uint offset,
            ushort length)
        {
            byte cmd = (byte)(command.Header.Command & (byte)ENetProtocolCommand.Mask);

            if (cmd == (byte)ENetProtocolCommand.None)
                return null;

            var outgoing = new ENetOutgoingCommand
            {
                Command = command,
                FragmentOffset = offset,
                FragmentLength = length,
                Packet = packet
            };

            byte channelId = command.Header.ChannelId;
            if (channelId < ChannelCount && Channels != null)
            {
                var channel = Channels[channelId];
                bool isReliable = (command.Header.Command & (byte)ENetProtocolFlag.CommandFlagAcknowledge) != 0;

                if (isReliable)
                {
                    channel.OutgoingReliableSequenceNumber++;
                    outgoing.ReliableSequenceNumber = channel.OutgoingReliableSequenceNumber;
                    outgoing.UnreliableSequenceNumber = 0;
                }
                else if ((command.Header.Command & (byte)ENetProtocolFlag.CommandFlagUnsequenced) != 0)
                {
                    OutgoingUnsequencedGroup++;
                    outgoing.ReliableSequenceNumber = 0;
                    outgoing.UnreliableSequenceNumber = 0;
                }
                else
                {
                    if ((cmd & (byte)ENetProtocolCommand.Mask) == (byte)ENetProtocolCommand.SendUnreliable)
                        channel.OutgoingUnreliableSequenceNumber++;
                    outgoing.ReliableSequenceNumber = channel.OutgoingReliableSequenceNumber;
                    outgoing.UnreliableSequenceNumber = channel.OutgoingUnreliableSequenceNumber;
                }
            }
            else
            {
                // System-level commands (e.g. connect, disconnect, ping)
                OutgoingReliableSequenceNumber++;
                outgoing.ReliableSequenceNumber = OutgoingReliableSequenceNumber;
                outgoing.UnreliableSequenceNumber = 0;
            }

            // Update command headers with sequence numbers
            outgoing.Command.Header.ReliableSequenceNumber = outgoing.ReliableSequenceNumber;

            SetupOutgoingCommand(outgoing);
            return outgoing;
        }

        /// <summary>
        /// Queue an ACK to be sent for a received reliable command.
        /// Mirrors enet_peer_queue_acknowledgement().
        /// </summary>
        public ENetAcknowledgement? QueueAcknowledgement(ref ENetProtocol command, ushort sentTime)
        {
            if (command.Header.ChannelId < ChannelCount && Channels != null)
            {
                var channel = Channels[command.Header.ChannelId];
                ushort reliableWindow = (ushort)(command.Header.ReliableSequenceNumber / ProtocolConstants.PeerReliableWindowSize);
                ushort currentWindow = (ushort)(channel.IncomingReliableSequenceNumber / ProtocolConstants.PeerReliableWindowSize);

                if (command.Header.ReliableSequenceNumber < channel.IncomingReliableSequenceNumber)
                    reliableWindow += (ushort)ProtocolConstants.PeerReliableWindows;

                if (reliableWindow >= currentWindow + ProtocolConstants.PeerFreeReliableWindows - 1 &&
                    reliableWindow <= currentWindow + ProtocolConstants.PeerFreeReliableWindows)
                    return null;
            }

            var ack = new ENetAcknowledgement
            {
                SentTime = sentTime,
                Command = command
            };

            OutgoingDataTotal += (uint)CommandSizes.GetCommandSize((byte)ENetProtocolCommand.Acknowledge);
            Acknowledgements.Insert(Acknowledgements.End, ack.ListNode);
            return ack;
        }

        /// <summary>
        /// Insert an incoming reliable command into the channel's reliable queue, ordered by sequence number.
        /// Returns the command if queued, null if duplicate/invalid.
        /// Mirrors enet_peer_queue_incoming_command().
        /// </summary>
        public ENetIncomingCommand? QueueIncomingCommand(
            ref ENetProtocol command,
            byte[]? data,
            int dataLength,
            ENetPacketFlag flags,
            uint fragmentCount)
        {
            ENetChannel? channel = null;
            byte channelId = command.Header.ChannelId;
            uint unreliableSequenceNumber = 0;
            uint reliableSequenceNumber = command.Header.ReliableSequenceNumber;
            ENetIncomingCommand? incomingCommand;
            ushort reliableWindow, currentWindow;

            if (State == ENetPeerState.DisconnectLater)
                goto freePacket;

            if ((command.Header.Command & (byte)ENetProtocolCommand.Mask) != (byte)ENetProtocolCommand.SendUnsequenced)
            {
                if (channelId >= ChannelCount || Channels == null)
                    goto freePacket;
                channel = Channels[channelId];
            }

            byte cmdType = (byte)(command.Header.Command & (byte)ENetProtocolCommand.Mask);

            if (cmdType == (byte)ENetProtocolCommand.SendUnreliable ||
                cmdType == (byte)ENetProtocolCommand.SendUnreliableFragment)
            {
                unreliableSequenceNumber = (ushort)(command.Header.ReliableSequenceNumber & 0xFFFF);
            }

            if (cmdType == (byte)ENetProtocolCommand.SendUnsequenced)
            {
                goto createPacket;
            }

            // Reliable: check window
            if (channel != null)
            {
                reliableWindow = (ushort)(reliableSequenceNumber / ProtocolConstants.PeerReliableWindowSize);
                currentWindow = (ushort)(channel.IncomingReliableSequenceNumber / ProtocolConstants.PeerReliableWindowSize);

                if (reliableSequenceNumber < channel.IncomingReliableSequenceNumber)
                    reliableWindow += (ushort)ProtocolConstants.PeerReliableWindows;

                if (reliableWindow < currentWindow ||
                    reliableWindow >= currentWindow + ProtocolConstants.PeerFreeReliableWindows - 1)
                    goto freePacket;

                if (cmdType == (byte)ENetProtocolCommand.SendUnreliable ||
                    cmdType == (byte)ENetProtocolCommand.SendUnreliableFragment)
                {
                    reliableSequenceNumber = channel.IncomingReliableSequenceNumber;
                    reliableWindow = (ushort)(reliableSequenceNumber / ProtocolConstants.PeerReliableWindowSize);

                    // Search for duplicate unreliable
                    for (var cur = channel.IncomingUnreliableCommands.Begin;
                         cur != channel.IncomingUnreliableCommands.End;
                         cur = cur.Next!)
                    {
                        var existing = cur.Owner!;
                        if (existing.ReliableSequenceNumber == reliableSequenceNumber &&
                            existing.UnreliableSequenceNumber == unreliableSequenceNumber)
                            goto freePacket;
                    }
                }
                else
                {
                    // Reliable: look for duplicate
                    for (var cur = channel.IncomingReliableCommands.Begin;
                         cur != channel.IncomingReliableCommands.End;
                         cur = cur.Next!)
                    {
                        var existing = cur.Owner!;
                        if (existing.ReliableSequenceNumber == command.Header.ReliableSequenceNumber)
                        {
                            if (existing.ReliableSequenceNumber <= channel.IncomingReliableSequenceNumber)
                                goto freePacket;
                            goto dispatchCommand;
                        }
                    }
                }
            }

        createPacket:
            ENetPacket? pkt = null;
            if (fragmentCount > 0 || (data != null && dataLength > 0))
                pkt = ENetPacket.Create(data, dataLength, flags);

            incomingCommand = new ENetIncomingCommand
            {
                ReliableSequenceNumber = command.Header.ReliableSequenceNumber,
                UnreliableSequenceNumber = (ushort)(unreliableSequenceNumber & 0xFFFF),
                Command = command,
                FragmentCount = fragmentCount,
                FragmentsRemaining = fragmentCount,
                Packet = pkt
            };

            if (fragmentCount > 0)
            {
                int bitmapSize = ((int)fragmentCount + 31) / 32;
                incomingCommand.Fragments = new uint[bitmapSize];
            }

            if (pkt != null)
            {
                pkt.ReferenceCount++;
                TotalWaitingData += pkt.DataLength;
            }

            // Enqueue into appropriate channel list
            if (channel != null)
            {
                if (cmdType == (byte)ENetProtocolCommand.SendUnreliable ||
                    cmdType == (byte)ENetProtocolCommand.SendUnreliableFragment)
                {
                    InsertOrderedUnreliable(channel.IncomingUnreliableCommands, incomingCommand);
                }
                else
                {
                    InsertOrderedReliable(channel.IncomingReliableCommands, incomingCommand);
                }
            }
            else
            {
                // Unsequenced – go straight to dispatchedCommands
                DispatchedCommands.Insert(DispatchedCommands.End, incomingCommand.ListNode);
            }

            return incomingCommand;

        dispatchCommand:
            return null;

        freePacket:
            return null;
        }

        private static void InsertOrderedReliable(ENetList<ENetIncomingCommand> list, ENetIncomingCommand cmd)
        {
            // Find insertion point (ascending reliable sequence number)
            var cur = list.End.Previous!;
            while (cur != list.Sentinel)
            {
                var item = cur.Owner;
                if (item != null)
                {
                    if (item.ReliableSequenceNumber <= cmd.ReliableSequenceNumber)
                        break;
                }
                cur = cur.Previous!;
            }
            list.Insert(cur.Next!, cmd.ListNode);
        }

        private static void InsertOrderedUnreliable(ENetList<ENetIncomingCommand> list, ENetIncomingCommand cmd)
        {
            var cur = list.End.Previous!;
            while (cur != list.Sentinel)
            {
                var item = cur.Owner;
                if (item != null)
                {
                    if (item.ReliableSequenceNumber <= cmd.ReliableSequenceNumber &&
                        item.UnreliableSequenceNumber <= cmd.UnreliableSequenceNumber)
                        break;
                }
                cur = cur.Previous!;
            }
            list.Insert(cur.Next!, cmd.ListNode);
        }

        /// <summary>
        /// Dispatch incoming reliable commands to the dispatched queue when they are in order.
        /// Mirrors enet_peer_dispatch_incoming_reliable_commands().
        /// </summary>
        public void DispatchIncomingReliableCommands(ENetChannel channel, ENetIncomingCommand? queuedCommand)
        {
            var cur = channel.IncomingReliableCommands.Begin;
            while (cur != channel.IncomingReliableCommands.End)
            {
                var incoming = cur.Owner!;
                cur = cur.Next!;

                if (incoming.FragmentsRemaining > 0 ||
                    incoming.ReliableSequenceNumber != (ushort)(channel.IncomingReliableSequenceNumber + 1))
                    break;

                channel.IncomingReliableSequenceNumber = incoming.ReliableSequenceNumber;

                if (incoming.FragmentCount > 0)
                    channel.IncomingReliableSequenceNumber += (ushort)(incoming.FragmentCount - 1);

                channel.IncomingReliableCommands.Remove(incoming.ListNode);

                if (incoming.Packet != null)
                    DispatchedCommands.Insert(DispatchedCommands.End, incoming.ListNode);
            }

            if (!NeedsDispatch && Host != null)
            {
                Host.DispatchQueue.Insert(Host.DispatchQueue.End, DispatchListNode);
                NeedsDispatch = true;
            }

            if (queuedCommand != null)
                DispatchIncomingUnreliableCommands(channel, null);
        }

        /// <summary>
        /// Dispatch ready unreliable commands to the dispatched queue.
        /// Mirrors enet_peer_dispatch_incoming_unreliable_commands().
        /// </summary>
        public void DispatchIncomingUnreliableCommands(ENetChannel channel, ENetIncomingCommand? queuedCommand)
        {
            ENetListNode<ENetIncomingCommand>? droppedCommand = null;
            ENetListNode<ENetIncomingCommand>? startCommand = null;

            var cur = channel.IncomingUnreliableCommands.Begin;
            while (cur != channel.IncomingUnreliableCommands.End)
            {
                var incoming = cur.Owner!;
                byte cmdType = (byte)(incoming.Command.Header.Command & (byte)ENetProtocolCommand.Mask);
                cur = cur.Next!;

                if (cmdType == (byte)ENetProtocolCommand.SendUnsequenced)
                    continue;

                if (incoming.ReliableSequenceNumber != channel.IncomingReliableSequenceNumber)
                    break;

                if (incoming.FragmentsRemaining > 0)
                {
                    if (startCommand == null) startCommand = incoming.ListNode;
                    continue;
                }

                startCommand = null;

                channel.IncomingUnreliableSequenceNumber = incoming.UnreliableSequenceNumber;
                channel.IncomingUnreliableCommands.Remove(incoming.ListNode);

                if (incoming.Packet != null)
                    DispatchedCommands.Insert(DispatchedCommands.End, incoming.ListNode);
                droppedCommand = incoming.ListNode;
            }

            if (!NeedsDispatch && Host != null && !DispatchedCommands.IsEmpty)
            {
                Host.DispatchQueue.Insert(Host.DispatchQueue.End, DispatchListNode);
                NeedsDispatch = true;
            }
        }

        /// <summary>
        /// Send a ping to the peer.
        /// </summary>
        public void Ping()
        {
            if (State != ENetPeerState.Connected) return;

            var cmd = new ENetProtocol();
            cmd.Header.Command = (byte)ENetProtocolCommand.Ping |
                                  (byte)ENetProtocolFlag.CommandFlagAcknowledge;
            cmd.Header.ChannelId = 0xFF;

            QueueOutgoingCommand(ref cmd, null, 0, 0);
        }

        public void SetPingInterval(uint interval)
        {
            PingInterval = interval > 0 ? interval : ProtocolConstants.PeerPingInterval;
        }

        public void SetTimeout(uint limit, uint minimum, uint maximum)
        {
            TimeoutLimit = limit != 0 ? limit : ProtocolConstants.PeerTimeoutLimit;
            TimeoutMinimum = minimum != 0 ? minimum : ProtocolConstants.PeerTimeoutMinimum;
            TimeoutMaximum = maximum != 0 ? maximum : ProtocolConstants.PeerTimeoutMaximum;
        }

        public void Disconnect(uint data) => Host?.PeerDisconnect(this, data, false, false);
        public void DisconnectNow(uint data) => Host?.PeerDisconnect(this, data, true, false);
        public void DisconnectLater(uint data) => Host?.PeerDisconnect(this, data, false, true);
    }
}
