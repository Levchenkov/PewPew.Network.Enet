using System;
using System.Buffers.Binary;

namespace PewPew.Network.Enet.Internal
{
    // =========================================================================
    // Protocol constants
    // =========================================================================

    internal static class ProtocolConstants
    {
        public const int MinimumMtu = 576;
        public const int MaximumMtu = 4096;
        public const int MaximumPacketCommands = 32;
        public const int MinimumWindowSize = 4096;
        public const int MaximumWindowSize = 65536;
        public const int MinimumChannelCount = 1;
        public const int MaximumChannelCount = 255;
        public const int MaximumPeerId = 0xFFF;
        public const int MaximumFragmentCount = 1024 * 1024;

        // Host defaults
        public const int HostBufferSizeMin = 256 * 1024;
        public const int HostBufferSizeMax = 1024 * 1024;
        public const int HostBandwidthThrottleInterval = 1000;
        public const int HostDefaultMtu = 1280;
        public const int HostDefaultMaxPacketSize = 32 * 1024 * 1024;
        public const int HostDefaultMaxWaitingData = 32 * 1024 * 1024;

        // Peer defaults
        public const uint PeerDefaultRoundTripTime = 1;
        public const uint PeerDefaultPacketThrottle = 32;
        public const uint PeerPacketThrottleThreshold = 40;
        public const uint PeerPacketThrottleScale = 32;
        public const uint PeerPacketThrottleCounter = 7;
        public const uint PeerPacketThrottleAcceleration = 2;
        public const uint PeerPacketThrottleDeceleration = 2;
        public const uint PeerPacketThrottleInterval = 5000;
        public const uint PeerWindowSizeScale = 64 * 1024;
        public const uint PeerTimeoutLimit = 32;
        public const uint PeerTimeoutMinimum = 5000;
        public const uint PeerTimeoutMaximum = 30000;
        public const uint PeerPingInterval = 250;
        public const int PeerUnsequencedWindows = 64;
        public const int PeerUnsequencedWindowSize = 1024;
        public const int PeerFreeUnsequencedWindows = 32;
        public const int PeerReliableWindows = 16;
        public const int PeerReliableWindowSize = 0x1000;
        public const int PeerFreeReliableWindows = 8;

        // Buffer maximum = 1 + 2 * MaximumPacketCommands
        public const int BufferMaximum = 1 + 2 * MaximumPacketCommands;
    }

    // =========================================================================
    // Protocol command enums
    // =========================================================================

    internal enum ENetProtocolCommand : byte
    {
        None = 0,
        Acknowledge = 1,
        Connect = 2,
        VerifyConnect = 3,
        Disconnect = 4,
        Ping = 5,
        SendReliable = 6,
        SendUnreliable = 7,
        SendFragment = 8,
        SendUnsequenced = 9,
        BandwidthLimit = 10,
        ThrottleConfigure = 11,
        SendUnreliableFragment = 12,
        Count = 13,
        Mask = 0x0F
    }

    [Flags]
    internal enum ENetProtocolFlag : ushort
    {
        CommandFlagAcknowledge = 1 << 7,
        CommandFlagUnsequenced = 1 << 6,
        HeaderFlagSentTime = 1 << 14,
        HeaderFlagMask = HeaderFlagSentTime,
        HeaderSessionMask = 3 << 12,
        HeaderSessionShift = 12
    }

    // =========================================================================
    // Wire-format packet sizes (matches sizeof() in C)
    // =========================================================================

    internal static class CommandSizes
    {
        // Must match sizeof(ENetProtocol*) in the C packed structs
        public static readonly int[] Sizes = new int[(int)ENetProtocolCommand.Count]
        {
            0,   // None
            8,   // Acknowledge
            48,  // Connect
            44,  // VerifyConnect
            8,   // Disconnect
            4,   // Ping
            6,   // SendReliable
            8,   // SendUnreliable
            24,  // SendFragment
            8,   // SendUnsequenced
            12,  // BandwidthLimit
            16,  // ThrottleConfigure
            24,  // SendUnreliableFragment
        };

        // Command header size: command(1) + channelID(1) + reliableSequenceNumber(2) = 4
        public const int CommandHeaderSize = 4;
        // Protocol header: peerID(2) + sentTime(2) = 4
        public const int ProtocolHeaderSize = 4;
        // Protocol header without sentTime: peerID(2) = 2  (when HEADER_FLAG_SENT_TIME not set)

        public static int GetCommandSize(byte command)
        {
            return (command & (byte)ENetProtocolCommand.Mask) switch
            {
                (byte)ENetProtocolCommand.Acknowledge => 8,     // header(4)+2+2
                (byte)ENetProtocolCommand.Connect => 48,        // header(4)+2+1+1+4*10+4 = 48
                (byte)ENetProtocolCommand.VerifyConnect => 44,  // header(4)+2+1+1+4*9 = 44
                (byte)ENetProtocolCommand.Disconnect => 8,      // header(4)+4
                (byte)ENetProtocolCommand.Ping => 4,            // header only
                (byte)ENetProtocolCommand.SendReliable => 6,    // header(4)+2
                (byte)ENetProtocolCommand.SendUnreliable => 8,  // header(4)+2+2
                (byte)ENetProtocolCommand.SendFragment => 24,   // header(4)+2+2+4+4+4+4
                (byte)ENetProtocolCommand.SendUnsequenced => 8, // header(4)+2+2
                (byte)ENetProtocolCommand.BandwidthLimit => 12, // header(4)+4+4
                (byte)ENetProtocolCommand.ThrottleConfigure => 16, // header(4)+4+4+4
                (byte)ENetProtocolCommand.SendUnreliableFragment => 24, // same as SendFragment
                _ => 0
            };
        }
    }

    // =========================================================================
    // Protocol packet serialization / deserialization
    // All values are in network byte order (big-endian) on the wire.
    // =========================================================================

    /// <summary>
    /// ENetProtocolHeader – 4 bytes (or 2 if no sentTime flag).
    /// peerID (2 bytes) | sentTime (2 bytes)
    /// </summary>
    internal struct ProtocolHeader
    {
        public ushort PeerId;   // upper 4 bits: flags/session; lower 12 bits: peer id
        public ushort SentTime;

        public static ProtocolHeader Read(ReadOnlySpan<byte> buf)
        {
            return new ProtocolHeader
            {
                PeerId = BinaryPrimitives.ReadUInt16BigEndian(buf),
                SentTime = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(2))
            };
        }

        public void Write(Span<byte> buf)
        {
            BinaryPrimitives.WriteUInt16BigEndian(buf, PeerId);
            BinaryPrimitives.WriteUInt16BigEndian(buf.Slice(2), SentTime);
        }
    }

    /// <summary>
    /// ENetProtocolCommandHeader – 4 bytes.
    /// command(1) | channelID(1) | reliableSequenceNumber(2)
    /// </summary>
    internal struct ProtocolCommandHeader
    {
        public byte Command;
        public byte ChannelId;
        public ushort ReliableSequenceNumber;

        public static ProtocolCommandHeader Read(ReadOnlySpan<byte> buf)
        {
            return new ProtocolCommandHeader
            {
                Command = buf[0],
                ChannelId = buf[1],
                ReliableSequenceNumber = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(2))
            };
        }

        public void Write(Span<byte> buf)
        {
            buf[0] = Command;
            buf[1] = ChannelId;
            BinaryPrimitives.WriteUInt16BigEndian(buf.Slice(2), ReliableSequenceNumber);
        }
    }

    internal struct ProtocolAcknowledge
    {
        public ProtocolCommandHeader Header;           // 4
        public ushort ReceivedReliableSequenceNumber;  // 2
        public ushort ReceivedSentTime;                // 2
        // Total: 8 bytes

        public static ProtocolAcknowledge Read(ReadOnlySpan<byte> buf)
        {
            return new ProtocolAcknowledge
            {
                Header = ProtocolCommandHeader.Read(buf),
                ReceivedReliableSequenceNumber = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(4)),
                ReceivedSentTime = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(6))
            };
        }

        public void Write(Span<byte> buf)
        {
            Header.Write(buf);
            BinaryPrimitives.WriteUInt16BigEndian(buf.Slice(4), ReceivedReliableSequenceNumber);
            BinaryPrimitives.WriteUInt16BigEndian(buf.Slice(6), ReceivedSentTime);
        }
    }

    internal struct ProtocolConnect
    {
        public ProtocolCommandHeader Header;           // 4
        public ushort OutgoingPeerId;                  // 2
        public byte IncomingSessionId;                 // 1
        public byte OutgoingSessionId;                 // 1
        public uint Mtu;                               // 4
        public uint WindowSize;                        // 4
        public uint ChannelCount;                      // 4
        public uint IncomingBandwidth;                 // 4
        public uint OutgoingBandwidth;                 // 4
        public uint PacketThrottleInterval;            // 4
        public uint PacketThrottleAcceleration;        // 4
        public uint PacketThrottleDeceleration;        // 4
        public uint ConnectId;                         // 4
        public uint Data;                              // 4
        // Total: 48 bytes

        public static ProtocolConnect Read(ReadOnlySpan<byte> buf)
        {
            var c = new ProtocolConnect();
            c.Header = ProtocolCommandHeader.Read(buf);
            c.OutgoingPeerId = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(4));
            c.IncomingSessionId = buf[6];
            c.OutgoingSessionId = buf[7];
            c.Mtu = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(8));
            c.WindowSize = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(12));
            c.ChannelCount = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(16));
            c.IncomingBandwidth = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(20));
            c.OutgoingBandwidth = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(24));
            c.PacketThrottleInterval = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(28));
            c.PacketThrottleAcceleration = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(32));
            c.PacketThrottleDeceleration = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(36));
            c.ConnectId = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(40));
            c.Data = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(44));
            return c;
        }

        public void Write(Span<byte> buf)
        {
            Header.Write(buf);
            BinaryPrimitives.WriteUInt16BigEndian(buf.Slice(4), OutgoingPeerId);
            buf[6] = IncomingSessionId;
            buf[7] = OutgoingSessionId;
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(8), Mtu);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(12), WindowSize);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(16), ChannelCount);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(20), IncomingBandwidth);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(24), OutgoingBandwidth);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(28), PacketThrottleInterval);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(32), PacketThrottleAcceleration);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(36), PacketThrottleDeceleration);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(40), ConnectId);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(44), Data);
        }
    }

    internal struct ProtocolVerifyConnect
    {
        public ProtocolCommandHeader Header;           // 4
        public ushort OutgoingPeerId;                  // 2
        public byte IncomingSessionId;                 // 1
        public byte OutgoingSessionId;                 // 1
        public uint Mtu;                               // 4
        public uint WindowSize;                        // 4
        public uint ChannelCount;                      // 4
        public uint IncomingBandwidth;                 // 4
        public uint OutgoingBandwidth;                 // 4
        public uint PacketThrottleInterval;            // 4
        public uint PacketThrottleAcceleration;        // 4
        public uint PacketThrottleDeceleration;        // 4
        public uint ConnectId;                         // 4
        // Total: 44 bytes

        public static ProtocolVerifyConnect Read(ReadOnlySpan<byte> buf)
        {
            var c = new ProtocolVerifyConnect();
            c.Header = ProtocolCommandHeader.Read(buf);
            c.OutgoingPeerId = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(4));
            c.IncomingSessionId = buf[6];
            c.OutgoingSessionId = buf[7];
            c.Mtu = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(8));
            c.WindowSize = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(12));
            c.ChannelCount = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(16));
            c.IncomingBandwidth = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(20));
            c.OutgoingBandwidth = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(24));
            c.PacketThrottleInterval = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(28));
            c.PacketThrottleAcceleration = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(32));
            c.PacketThrottleDeceleration = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(36));
            c.ConnectId = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(40));
            return c;
        }

        public void Write(Span<byte> buf)
        {
            Header.Write(buf);
            BinaryPrimitives.WriteUInt16BigEndian(buf.Slice(4), OutgoingPeerId);
            buf[6] = IncomingSessionId;
            buf[7] = OutgoingSessionId;
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(8), Mtu);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(12), WindowSize);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(16), ChannelCount);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(20), IncomingBandwidth);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(24), OutgoingBandwidth);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(28), PacketThrottleInterval);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(32), PacketThrottleAcceleration);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(36), PacketThrottleDeceleration);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(40), ConnectId);
        }
    }

    internal struct ProtocolBandwidthLimit
    {
        public ProtocolCommandHeader Header;  // 4
        public uint IncomingBandwidth;         // 4
        public uint OutgoingBandwidth;         // 4
        // Total: 12

        public static ProtocolBandwidthLimit Read(ReadOnlySpan<byte> buf) => new ProtocolBandwidthLimit
        {
            Header = ProtocolCommandHeader.Read(buf),
            IncomingBandwidth = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(4)),
            OutgoingBandwidth = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(8))
        };

        public void Write(Span<byte> buf)
        {
            Header.Write(buf);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(4), IncomingBandwidth);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(8), OutgoingBandwidth);
        }
    }

    internal struct ProtocolThrottleConfigure
    {
        public ProtocolCommandHeader Header;    // 4
        public uint PacketThrottleInterval;     // 4
        public uint PacketThrottleAcceleration; // 4
        public uint PacketThrottleDeceleration; // 4
        // Total: 16

        public static ProtocolThrottleConfigure Read(ReadOnlySpan<byte> buf) => new ProtocolThrottleConfigure
        {
            Header = ProtocolCommandHeader.Read(buf),
            PacketThrottleInterval = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(4)),
            PacketThrottleAcceleration = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(8)),
            PacketThrottleDeceleration = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(12))
        };

        public void Write(Span<byte> buf)
        {
            Header.Write(buf);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(4), PacketThrottleInterval);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(8), PacketThrottleAcceleration);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(12), PacketThrottleDeceleration);
        }
    }

    internal struct ProtocolDisconnect
    {
        public ProtocolCommandHeader Header; // 4
        public uint Data;                    // 4
        // Total: 8

        public static ProtocolDisconnect Read(ReadOnlySpan<byte> buf) => new ProtocolDisconnect
        {
            Header = ProtocolCommandHeader.Read(buf),
            Data = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(4))
        };

        public void Write(Span<byte> buf)
        {
            Header.Write(buf);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(4), Data);
        }
    }

    internal struct ProtocolPing
    {
        public ProtocolCommandHeader Header; // 4
        // Total: 4

        public static ProtocolPing Read(ReadOnlySpan<byte> buf) => new ProtocolPing
        {
            Header = ProtocolCommandHeader.Read(buf)
        };

        public void Write(Span<byte> buf) => Header.Write(buf);
    }

    internal struct ProtocolSendReliable
    {
        public ProtocolCommandHeader Header; // 4
        public ushort DataLength;            // 2
        // Total: 6

        public static ProtocolSendReliable Read(ReadOnlySpan<byte> buf) => new ProtocolSendReliable
        {
            Header = ProtocolCommandHeader.Read(buf),
            DataLength = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(4))
        };

        public void Write(Span<byte> buf)
        {
            Header.Write(buf);
            BinaryPrimitives.WriteUInt16BigEndian(buf.Slice(4), DataLength);
        }
    }

    internal struct ProtocolSendUnreliable
    {
        public ProtocolCommandHeader Header;   // 4
        public ushort UnreliableSequenceNumber; // 2
        public ushort DataLength;               // 2
        // Total: 8

        public static ProtocolSendUnreliable Read(ReadOnlySpan<byte> buf) => new ProtocolSendUnreliable
        {
            Header = ProtocolCommandHeader.Read(buf),
            UnreliableSequenceNumber = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(4)),
            DataLength = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(6))
        };

        public void Write(Span<byte> buf)
        {
            Header.Write(buf);
            BinaryPrimitives.WriteUInt16BigEndian(buf.Slice(4), UnreliableSequenceNumber);
            BinaryPrimitives.WriteUInt16BigEndian(buf.Slice(6), DataLength);
        }
    }

    internal struct ProtocolSendUnsequenced
    {
        public ProtocolCommandHeader Header; // 4
        public ushort UnsequencedGroup;      // 2
        public ushort DataLength;            // 2
        // Total: 8

        public static ProtocolSendUnsequenced Read(ReadOnlySpan<byte> buf) => new ProtocolSendUnsequenced
        {
            Header = ProtocolCommandHeader.Read(buf),
            UnsequencedGroup = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(4)),
            DataLength = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(6))
        };

        public void Write(Span<byte> buf)
        {
            Header.Write(buf);
            BinaryPrimitives.WriteUInt16BigEndian(buf.Slice(4), UnsequencedGroup);
            BinaryPrimitives.WriteUInt16BigEndian(buf.Slice(6), DataLength);
        }
    }

    internal struct ProtocolSendFragment
    {
        public ProtocolCommandHeader Header; // 4
        public ushort StartSequenceNumber;   // 2
        public ushort DataLength;            // 2
        public uint FragmentCount;           // 4
        public uint FragmentNumber;          // 4
        public uint TotalLength;             // 4
        public uint FragmentOffset;          // 4
        // Total: 24

        public static ProtocolSendFragment Read(ReadOnlySpan<byte> buf)
        {
            var f = new ProtocolSendFragment();
            f.Header = ProtocolCommandHeader.Read(buf);
            f.StartSequenceNumber = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(4));
            f.DataLength = BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(6));
            f.FragmentCount = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(8));
            f.FragmentNumber = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(12));
            f.TotalLength = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(16));
            f.FragmentOffset = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(20));
            return f;
        }

        public void Write(Span<byte> buf)
        {
            Header.Write(buf);
            BinaryPrimitives.WriteUInt16BigEndian(buf.Slice(4), StartSequenceNumber);
            BinaryPrimitives.WriteUInt16BigEndian(buf.Slice(6), DataLength);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(8), FragmentCount);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(12), FragmentNumber);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(16), TotalLength);
            BinaryPrimitives.WriteUInt32BigEndian(buf.Slice(20), FragmentOffset);
        }
    }

    /// <summary>
    /// Tagged union matching C ENetProtocol union. Stores whichever command type is active.
    /// We use a command byte to discriminate.
    /// </summary>
    internal struct ENetProtocol
    {
        // Always present
        public ProtocolCommandHeader Header;

        // Union fields – only one is valid at a time (based on Header.Command)
        public ProtocolAcknowledge Acknowledge;
        public ProtocolConnect Connect;
        public ProtocolVerifyConnect VerifyConnect;
        public ProtocolDisconnect Disconnect;
        public ProtocolPing Ping;
        public ProtocolSendReliable SendReliable;
        public ProtocolSendUnreliable SendUnreliable;
        public ProtocolSendUnsequenced SendUnsequenced;
        public ProtocolSendFragment SendFragment;
        public ProtocolBandwidthLimit BandwidthLimit;
        public ProtocolThrottleConfigure ThrottleConfigure;

        public ENetProtocolCommand CommandType =>
            (ENetProtocolCommand)(Header.Command & (byte)ENetProtocolCommand.Mask);
    }
}
