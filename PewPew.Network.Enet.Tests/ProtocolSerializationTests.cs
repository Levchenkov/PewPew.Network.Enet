using System;
using Xunit;
using PewPew.Network.Enet.Internal;

namespace PewPew.Network.Enet.Tests
{
    public class ProtocolSerializationTests
    {
        // ── ProtocolConstants ─────────────────────────────────────────────────

        [Fact]
        public void ProtocolConstants_MinimumMtu_Is576()
        {
            Assert.Equal(576, ProtocolConstants.MinimumMtu);
        }

        [Fact]
        public void ProtocolConstants_MaximumMtu_Is4096()
        {
            Assert.Equal(4096, ProtocolConstants.MaximumMtu);
        }

        [Fact]
        public void ProtocolConstants_MaximumChannelCount_Is255()
        {
            Assert.Equal(255, ProtocolConstants.MaximumChannelCount);
        }

        [Fact]
        public void ProtocolConstants_MinimumChannelCount_Is1()
        {
            Assert.Equal(1, ProtocolConstants.MinimumChannelCount);
        }

        [Fact]
        public void ProtocolConstants_MaximumPeerId_Is0xFFF()
        {
            Assert.Equal(0xFFF, ProtocolConstants.MaximumPeerId);
        }

        [Fact]
        public void ProtocolConstants_HostDefaultMtu_Is1280()
        {
            Assert.Equal(1280, ProtocolConstants.HostDefaultMtu);
        }

        // ── CommandSizes.GetCommandSize ────────────────────────────────────────

        [Theory]
        [InlineData((byte)ENetProtocolCommand.Acknowledge,     8)]
        [InlineData((byte)ENetProtocolCommand.Connect,         48)]
        [InlineData((byte)ENetProtocolCommand.VerifyConnect,   44)]
        [InlineData((byte)ENetProtocolCommand.Disconnect,      8)]
        [InlineData((byte)ENetProtocolCommand.Ping,            4)]
        [InlineData((byte)ENetProtocolCommand.SendReliable,    6)]
        [InlineData((byte)ENetProtocolCommand.SendUnreliable,  8)]
        [InlineData((byte)ENetProtocolCommand.SendFragment,    24)]
        [InlineData((byte)ENetProtocolCommand.SendUnsequenced, 8)]
        [InlineData((byte)ENetProtocolCommand.BandwidthLimit,  12)]
        [InlineData((byte)ENetProtocolCommand.ThrottleConfigure, 16)]
        [InlineData((byte)ENetProtocolCommand.SendUnreliableFragment, 24)]
        public void GetCommandSize_ReturnsExpectedSize(byte command, int expectedSize)
        {
            Assert.Equal(expectedSize, CommandSizes.GetCommandSize(command));
        }

        [Fact]
        public void GetCommandSize_UnknownCommand_ReturnsZero()
        {
            Assert.Equal(0, CommandSizes.GetCommandSize(0xFF));
        }

        [Fact]
        public void CommandSizes_SizesArray_MatchesGetCommandSize()
        {
            for (int i = 0; i < CommandSizes.Sizes.Length; i++)
            {
                int expected = CommandSizes.GetCommandSize((byte)i);
                Assert.Equal(expected, CommandSizes.Sizes[i]);
            }
        }

        // ── ProtocolHeader Read/Write round-trip ───────────────────────────────

        [Fact]
        public void ProtocolHeader_ReadWrite_RoundTrip()
        {
            var original = new ProtocolHeader
            {
                PeerId = 0x1234,
                SentTime = 0xABCD
            };

            byte[] buf = new byte[4];
            original.Write(buf);

            var restored = ProtocolHeader.Read(buf);

            Assert.Equal(original.PeerId, restored.PeerId);
            Assert.Equal(original.SentTime, restored.SentTime);
        }

        [Fact]
        public void ProtocolHeader_Write_IsBigEndian()
        {
            var h = new ProtocolHeader { PeerId = 0x0102, SentTime = 0x0304 };
            byte[] buf = new byte[4];
            h.Write(buf);

            Assert.Equal(0x01, buf[0]);
            Assert.Equal(0x02, buf[1]);
            Assert.Equal(0x03, buf[2]);
            Assert.Equal(0x04, buf[3]);
        }

        // ── ProtocolCommandHeader Read/Write round-trip ────────────────────────

        [Fact]
        public void ProtocolCommandHeader_ReadWrite_RoundTrip()
        {
            var original = new ProtocolCommandHeader
            {
                Command = 0x82,
                ChannelId = 0x03,
                ReliableSequenceNumber = 0x1234
            };

            byte[] buf = new byte[4];
            original.Write(buf);
            var restored = ProtocolCommandHeader.Read(buf);

            Assert.Equal(original.Command, restored.Command);
            Assert.Equal(original.ChannelId, restored.ChannelId);
            Assert.Equal(original.ReliableSequenceNumber, restored.ReliableSequenceNumber);
        }

        // ── ProtocolAcknowledge Read/Write round-trip ──────────────────────────

        [Fact]
        public void ProtocolAcknowledge_ReadWrite_RoundTrip()
        {
            var original = new ProtocolAcknowledge
            {
                Header = new ProtocolCommandHeader
                {
                    Command = (byte)ENetProtocolCommand.Acknowledge,
                    ChannelId = 0xFF,
                    ReliableSequenceNumber = 1
                },
                ReceivedReliableSequenceNumber = 42,
                ReceivedSentTime = 999
            };

            byte[] buf = new byte[8];
            original.Write(buf);
            var restored = ProtocolAcknowledge.Read(buf);

            Assert.Equal(original.Header.Command, restored.Header.Command);
            Assert.Equal(original.ReceivedReliableSequenceNumber, restored.ReceivedReliableSequenceNumber);
            Assert.Equal(original.ReceivedSentTime, restored.ReceivedSentTime);
        }

        // ── ProtocolConnect Read/Write round-trip ──────────────────────────────

        [Fact]
        public void ProtocolConnect_ReadWrite_RoundTrip()
        {
            var original = new ProtocolConnect
            {
                Header = new ProtocolCommandHeader
                {
                    Command = (byte)ENetProtocolCommand.Connect | (byte)ENetProtocolFlag.CommandFlagAcknowledge,
                    ChannelId = 0xFF,
                    ReliableSequenceNumber = 1
                },
                OutgoingPeerId = 7,
                IncomingSessionId = 1,
                OutgoingSessionId = 2,
                Mtu = 1280,
                WindowSize = 32768,
                ChannelCount = 2,
                IncomingBandwidth = 0,
                OutgoingBandwidth = 0,
                PacketThrottleInterval = 5000,
                PacketThrottleAcceleration = 2,
                PacketThrottleDeceleration = 2,
                ConnectId = 0xDEADBEEF,
                Data = 0x12345678
            };

            byte[] buf = new byte[48];
            original.Write(buf);
            var restored = ProtocolConnect.Read(buf);

            Assert.Equal(original.OutgoingPeerId, restored.OutgoingPeerId);
            Assert.Equal(original.Mtu, restored.Mtu);
            Assert.Equal(original.WindowSize, restored.WindowSize);
            Assert.Equal(original.ChannelCount, restored.ChannelCount);
            Assert.Equal(original.ConnectId, restored.ConnectId);
            Assert.Equal(original.Data, restored.Data);
        }

        // ── ProtocolVerifyConnect Read/Write round-trip ───────────────────────

        [Fact]
        public void ProtocolVerifyConnect_ReadWrite_RoundTrip()
        {
            var original = new ProtocolVerifyConnect
            {
                Header = new ProtocolCommandHeader
                {
                    Command = (byte)ENetProtocolCommand.VerifyConnect | (byte)ENetProtocolFlag.CommandFlagAcknowledge,
                    ChannelId = 0xFF,
                    ReliableSequenceNumber = 1
                },
                OutgoingPeerId = 3,
                Mtu = 1280,
                WindowSize = 65536,
                ChannelCount = 1,
                ConnectId = 0xCAFEBABE
            };

            byte[] buf = new byte[44];
            original.Write(buf);
            var restored = ProtocolVerifyConnect.Read(buf);

            Assert.Equal(original.OutgoingPeerId, restored.OutgoingPeerId);
            Assert.Equal(original.Mtu, restored.Mtu);
            Assert.Equal(original.ConnectId, restored.ConnectId);
        }

        // ── ProtocolDisconnect Read/Write round-trip ──────────────────────────

        [Fact]
        public void ProtocolDisconnect_ReadWrite_RoundTrip()
        {
            var original = new ProtocolDisconnect
            {
                Header = new ProtocolCommandHeader
                {
                    Command = (byte)ENetProtocolCommand.Disconnect | (byte)ENetProtocolFlag.CommandFlagAcknowledge,
                    ChannelId = 0xFF
                },
                Data = 0x99
            };

            byte[] buf = new byte[8];
            original.Write(buf);
            var restored = ProtocolDisconnect.Read(buf);

            Assert.Equal(original.Data, restored.Data);
        }

        // ── ProtocolPing Read/Write round-trip ────────────────────────────────

        [Fact]
        public void ProtocolPing_ReadWrite_RoundTrip()
        {
            var original = new ProtocolPing
            {
                Header = new ProtocolCommandHeader
                {
                    Command = (byte)ENetProtocolCommand.Ping | (byte)ENetProtocolFlag.CommandFlagAcknowledge,
                    ChannelId = 0xFF,
                    ReliableSequenceNumber = 5
                }
            };

            byte[] buf = new byte[4];
            original.Write(buf);
            var restored = ProtocolPing.Read(buf);

            Assert.Equal(original.Header.Command, restored.Header.Command);
            Assert.Equal(original.Header.ReliableSequenceNumber, restored.Header.ReliableSequenceNumber);
        }

        // ── ProtocolSendReliable Read/Write round-trip ────────────────────────

        [Fact]
        public void ProtocolSendReliable_ReadWrite_RoundTrip()
        {
            var original = new ProtocolSendReliable
            {
                Header = new ProtocolCommandHeader
                {
                    Command = (byte)ENetProtocolCommand.SendReliable | (byte)ENetProtocolFlag.CommandFlagAcknowledge,
                    ChannelId = 0,
                    ReliableSequenceNumber = 10
                },
                DataLength = 256
            };

            byte[] buf = new byte[6];
            original.Write(buf);
            var restored = ProtocolSendReliable.Read(buf);

            Assert.Equal(original.DataLength, restored.DataLength);
        }

        // ── ProtocolSendUnreliable Read/Write round-trip ──────────────────────

        [Fact]
        public void ProtocolSendUnreliable_ReadWrite_RoundTrip()
        {
            var original = new ProtocolSendUnreliable
            {
                Header = new ProtocolCommandHeader
                {
                    Command = (byte)ENetProtocolCommand.SendUnreliable,
                    ChannelId = 1
                },
                UnreliableSequenceNumber = 7,
                DataLength = 100
            };

            byte[] buf = new byte[8];
            original.Write(buf);
            var restored = ProtocolSendUnreliable.Read(buf);

            Assert.Equal(original.UnreliableSequenceNumber, restored.UnreliableSequenceNumber);
            Assert.Equal(original.DataLength, restored.DataLength);
        }

        // ── ProtocolSendUnsequenced Read/Write round-trip ─────────────────────

        [Fact]
        public void ProtocolSendUnsequenced_ReadWrite_RoundTrip()
        {
            var original = new ProtocolSendUnsequenced
            {
                Header = new ProtocolCommandHeader
                {
                    Command = (byte)ENetProtocolCommand.SendUnsequenced | (byte)ENetProtocolFlag.CommandFlagUnsequenced
                },
                UnsequencedGroup = 12,
                DataLength = 50
            };

            byte[] buf = new byte[8];
            original.Write(buf);
            var restored = ProtocolSendUnsequenced.Read(buf);

            Assert.Equal(original.UnsequencedGroup, restored.UnsequencedGroup);
            Assert.Equal(original.DataLength, restored.DataLength);
        }

        // ── ProtocolSendFragment Read/Write round-trip ────────────────────────

        [Fact]
        public void ProtocolSendFragment_ReadWrite_RoundTrip()
        {
            var original = new ProtocolSendFragment
            {
                Header = new ProtocolCommandHeader
                {
                    Command = (byte)ENetProtocolCommand.SendFragment | (byte)ENetProtocolFlag.CommandFlagAcknowledge,
                    ChannelId = 0
                },
                StartSequenceNumber = 1,
                DataLength = 500,
                FragmentCount = 3,
                FragmentNumber = 2,
                TotalLength = 1500,
                FragmentOffset = 1000
            };

            byte[] buf = new byte[24];
            original.Write(buf);
            var restored = ProtocolSendFragment.Read(buf);

            Assert.Equal(original.StartSequenceNumber, restored.StartSequenceNumber);
            Assert.Equal(original.DataLength, restored.DataLength);
            Assert.Equal(original.FragmentCount, restored.FragmentCount);
            Assert.Equal(original.FragmentNumber, restored.FragmentNumber);
            Assert.Equal(original.TotalLength, restored.TotalLength);
            Assert.Equal(original.FragmentOffset, restored.FragmentOffset);
        }

        // ── ProtocolBandwidthLimit Read/Write round-trip ──────────────────────

        [Fact]
        public void ProtocolBandwidthLimit_ReadWrite_RoundTrip()
        {
            var original = new ProtocolBandwidthLimit
            {
                Header = new ProtocolCommandHeader
                {
                    Command = (byte)ENetProtocolCommand.BandwidthLimit | (byte)ENetProtocolFlag.CommandFlagAcknowledge,
                    ChannelId = 0xFF
                },
                IncomingBandwidth = 1_000_000,
                OutgoingBandwidth = 500_000
            };

            byte[] buf = new byte[12];
            original.Write(buf);
            var restored = ProtocolBandwidthLimit.Read(buf);

            Assert.Equal(original.IncomingBandwidth, restored.IncomingBandwidth);
            Assert.Equal(original.OutgoingBandwidth, restored.OutgoingBandwidth);
        }

        // ── ProtocolThrottleConfigure Read/Write round-trip ───────────────────

        [Fact]
        public void ProtocolThrottleConfigure_ReadWrite_RoundTrip()
        {
            var original = new ProtocolThrottleConfigure
            {
                Header = new ProtocolCommandHeader
                {
                    Command = (byte)ENetProtocolCommand.ThrottleConfigure | (byte)ENetProtocolFlag.CommandFlagAcknowledge,
                    ChannelId = 0xFF
                },
                PacketThrottleInterval = 5000,
                PacketThrottleAcceleration = 2,
                PacketThrottleDeceleration = 2
            };

            byte[] buf = new byte[16];
            original.Write(buf);
            var restored = ProtocolThrottleConfigure.Read(buf);

            Assert.Equal(original.PacketThrottleInterval, restored.PacketThrottleInterval);
            Assert.Equal(original.PacketThrottleAcceleration, restored.PacketThrottleAcceleration);
            Assert.Equal(original.PacketThrottleDeceleration, restored.PacketThrottleDeceleration);
        }

        // ── ENetProtocol.CommandType ──────────────────────────────────────────

        [Fact]
        public void ENetProtocol_CommandType_ExtractsLowerNibble()
        {
            var p = new ENetProtocol();
            p.Header.Command = (byte)ENetProtocolCommand.Connect | (byte)ENetProtocolFlag.CommandFlagAcknowledge;

            Assert.Equal(ENetProtocolCommand.Connect, p.CommandType);
        }

        // ── ProtocolConstants — additional constants ───────────────────────────

        [Fact]
        public void ProtocolConstants_MaximumPacketCommands_Is32()
        {
            Assert.Equal(32, ProtocolConstants.MaximumPacketCommands);
        }

        [Fact]
        public void ProtocolConstants_MinimumWindowSize_Is4096()
        {
            Assert.Equal(4096, ProtocolConstants.MinimumWindowSize);
        }

        [Fact]
        public void ProtocolConstants_MaximumWindowSize_Is65536()
        {
            Assert.Equal(65536, ProtocolConstants.MaximumWindowSize);
        }

        [Fact]
        public void ProtocolConstants_MaximumFragmentCount_Is1MiB()
        {
            Assert.Equal(1024 * 1024, ProtocolConstants.MaximumFragmentCount);
        }

        [Fact]
        public void ProtocolConstants_PeerDefaultRoundTripTime_Is500()
        {
            Assert.Equal(500u, ProtocolConstants.PeerDefaultRoundTripTime);
        }

        [Fact]
        public void ProtocolConstants_PeerPingInterval_Is250()
        {
            Assert.Equal(250u, ProtocolConstants.PeerPingInterval);
        }

        [Fact]
        public void ProtocolConstants_PeerTimeoutMinimum_Is5000()
        {
            Assert.Equal(5000u, ProtocolConstants.PeerTimeoutMinimum);
        }

        [Fact]
        public void ProtocolConstants_PeerTimeoutMaximum_Is30000()
        {
            Assert.Equal(30000u, ProtocolConstants.PeerTimeoutMaximum);
        }

        [Fact]
        public void ProtocolConstants_BufferMaximum_Is65()
        {
            // BufferMaximum = 1 + 2 * 32 = 65
            Assert.Equal(65, ProtocolConstants.BufferMaximum);
        }

        // ── CommandSizes constants ─────────────────────────────────────────────

        [Fact]
        public void CommandSizes_CommandHeaderSize_Is4()
        {
            Assert.Equal(4, CommandSizes.CommandHeaderSize);
        }

        [Fact]
        public void CommandSizes_ProtocolHeaderSize_Is4()
        {
            Assert.Equal(4, CommandSizes.ProtocolHeaderSize);
        }

        // ── ENetProtocolFlag bit values ────────────────────────────────────────

        [Fact]
        public void ENetProtocolFlag_CommandFlagAcknowledge_Is0x80()
        {
            Assert.Equal(0x80, (int)ENetProtocolFlag.CommandFlagAcknowledge);
        }

        [Fact]
        public void ENetProtocolFlag_CommandFlagUnsequenced_Is0x40()
        {
            Assert.Equal(0x40, (int)ENetProtocolFlag.CommandFlagUnsequenced);
        }

        [Fact]
        public void ENetProtocolFlag_HeaderFlagSentTime_Is0x4000()
        {
            Assert.Equal(0x4000, (int)ENetProtocolFlag.HeaderFlagSentTime);
        }

        [Fact]
        public void ENetProtocolFlag_HeaderFlagMask_EqualsHeaderFlagSentTime()
        {
            Assert.Equal(ENetProtocolFlag.HeaderFlagSentTime, ENetProtocolFlag.HeaderFlagMask);
        }

        [Fact]
        public void ENetProtocolFlag_HeaderSessionShift_Is12()
        {
            Assert.Equal(12, (int)ENetProtocolFlag.HeaderSessionShift);
        }

        // ── PeerId session field extraction ───────────────────────────────────

        [Fact]
        public void ProtocolHeader_PeerId_SessionBitsAreInUpperNibble()
        {
            // PeerId = session(4 bits) << 12 | flags(2 bits) << 14 | peerId(12 bits)
            // Set session = 2 (0b10) at bits 12-13
            ushort sessionValue = 2;
            ushort rawPeerId = (ushort)((sessionValue << (int)ENetProtocolFlag.HeaderSessionShift) | 0x001);

            int extractedSession = (rawPeerId & (int)ENetProtocolFlag.HeaderSessionMask)
                                   >> (int)ENetProtocolFlag.HeaderSessionShift;

            Assert.Equal(sessionValue, (ushort)extractedSession);
        }

        // ── Boundary values round-trips ────────────────────────────────────────

        [Fact]
        public void ProtocolAcknowledge_BoundaryValues_RoundTrip()
        {
            var original = new ProtocolAcknowledge
            {
                Header = new ProtocolCommandHeader
                {
                    Command = 0xFF,
                    ChannelId = 0xFF,
                    ReliableSequenceNumber = 0xFFFF
                },
                ReceivedReliableSequenceNumber = 0xFFFF,
                ReceivedSentTime = 0xFFFF
            };

            byte[] buf = new byte[8];
            original.Write(buf);
            var restored = ProtocolAcknowledge.Read(buf);

            Assert.Equal(original.Header.Command, restored.Header.Command);
            Assert.Equal(original.Header.ChannelId, restored.Header.ChannelId);
            Assert.Equal(original.Header.ReliableSequenceNumber, restored.Header.ReliableSequenceNumber);
            Assert.Equal(original.ReceivedReliableSequenceNumber, restored.ReceivedReliableSequenceNumber);
            Assert.Equal(original.ReceivedSentTime, restored.ReceivedSentTime);
        }

        [Fact]
        public void ProtocolAcknowledge_ZeroValues_RoundTrip()
        {
            var original = new ProtocolAcknowledge();
            byte[] buf = new byte[8];
            original.Write(buf);
            var restored = ProtocolAcknowledge.Read(buf);

            Assert.Equal(0, restored.Header.Command);
            Assert.Equal(0, restored.Header.ChannelId);
            Assert.Equal(0, restored.Header.ReliableSequenceNumber);
            Assert.Equal(0, restored.ReceivedReliableSequenceNumber);
            Assert.Equal(0, restored.ReceivedSentTime);
        }

        [Fact]
        public void ProtocolConnect_AllFields_RoundTrip()
        {
            var original = new ProtocolConnect
            {
                Header = new ProtocolCommandHeader { Command = 0x82, ChannelId = 0xFF, ReliableSequenceNumber = 0x1234 },
                OutgoingPeerId = 0xABCD,
                IncomingSessionId = 0x01,
                OutgoingSessionId = 0x02,
                Mtu = 1400,
                WindowSize = 65536,
                ChannelCount = 4,
                IncomingBandwidth = 100_000,
                OutgoingBandwidth = 200_000,
                PacketThrottleInterval = 5000,
                PacketThrottleAcceleration = 2,
                PacketThrottleDeceleration = 2,
                ConnectId = 0xDEADBEEF,
                Data = 0x12345678
            };

            byte[] buf = new byte[48];
            original.Write(buf);
            var restored = ProtocolConnect.Read(buf);

            Assert.Equal(original.Header.ReliableSequenceNumber, restored.Header.ReliableSequenceNumber);
            Assert.Equal(original.OutgoingPeerId, restored.OutgoingPeerId);
            Assert.Equal(original.IncomingSessionId, restored.IncomingSessionId);
            Assert.Equal(original.OutgoingSessionId, restored.OutgoingSessionId);
            Assert.Equal(original.Mtu, restored.Mtu);
            Assert.Equal(original.WindowSize, restored.WindowSize);
            Assert.Equal(original.ChannelCount, restored.ChannelCount);
            Assert.Equal(original.IncomingBandwidth, restored.IncomingBandwidth);
            Assert.Equal(original.OutgoingBandwidth, restored.OutgoingBandwidth);
            Assert.Equal(original.PacketThrottleInterval, restored.PacketThrottleInterval);
            Assert.Equal(original.PacketThrottleAcceleration, restored.PacketThrottleAcceleration);
            Assert.Equal(original.PacketThrottleDeceleration, restored.PacketThrottleDeceleration);
            Assert.Equal(original.ConnectId, restored.ConnectId);
            Assert.Equal(original.Data, restored.Data);
        }

        [Fact]
        public void ProtocolConnect_BoundaryValues_RoundTrip()
        {
            var original = new ProtocolConnect
            {
                OutgoingPeerId = 0xFFFF,
                IncomingSessionId = 0xFF,
                OutgoingSessionId = 0xFF,
                Mtu = 0xFFFFFFFF,
                WindowSize = 0xFFFFFFFF,
                ChannelCount = 0xFF,
                IncomingBandwidth = 0xFFFFFFFF,
                OutgoingBandwidth = 0xFFFFFFFF,
                PacketThrottleInterval = 0xFFFFFFFF,
                PacketThrottleAcceleration = 0xFFFFFFFF,
                PacketThrottleDeceleration = 0xFFFFFFFF,
                ConnectId = 0xFFFFFFFF,
                Data = 0xFFFFFFFF
            };

            byte[] buf = new byte[48];
            original.Write(buf);
            var restored = ProtocolConnect.Read(buf);

            Assert.Equal(original.OutgoingPeerId, restored.OutgoingPeerId);
            Assert.Equal(original.IncomingSessionId, restored.IncomingSessionId);
            Assert.Equal(original.OutgoingSessionId, restored.OutgoingSessionId);
            Assert.Equal(original.Mtu, restored.Mtu);
            Assert.Equal(original.ConnectId, restored.ConnectId);
            Assert.Equal(original.Data, restored.Data);
        }

        [Fact]
        public void ProtocolVerifyConnect_AllFields_RoundTrip()
        {
            var original = new ProtocolVerifyConnect
            {
                Header = new ProtocolCommandHeader { Command = 0x83, ChannelId = 0xFF, ReliableSequenceNumber = 1 },
                OutgoingPeerId = 0x1234,
                IncomingSessionId = 0x01,
                OutgoingSessionId = 0x02,
                Mtu = 1280,
                WindowSize = 32768,
                ChannelCount = 2,
                IncomingBandwidth = 50_000,
                OutgoingBandwidth = 75_000,
                PacketThrottleInterval = 5000,
                PacketThrottleAcceleration = 2,
                PacketThrottleDeceleration = 2,
                ConnectId = 0xCAFEBABE
            };

            byte[] buf = new byte[44];
            original.Write(buf);
            var restored = ProtocolVerifyConnect.Read(buf);

            Assert.Equal(original.OutgoingPeerId, restored.OutgoingPeerId);
            Assert.Equal(original.IncomingSessionId, restored.IncomingSessionId);
            Assert.Equal(original.OutgoingSessionId, restored.OutgoingSessionId);
            Assert.Equal(original.Mtu, restored.Mtu);
            Assert.Equal(original.WindowSize, restored.WindowSize);
            Assert.Equal(original.ChannelCount, restored.ChannelCount);
            Assert.Equal(original.IncomingBandwidth, restored.IncomingBandwidth);
            Assert.Equal(original.OutgoingBandwidth, restored.OutgoingBandwidth);
            Assert.Equal(original.PacketThrottleInterval, restored.PacketThrottleInterval);
            Assert.Equal(original.PacketThrottleAcceleration, restored.PacketThrottleAcceleration);
            Assert.Equal(original.PacketThrottleDeceleration, restored.PacketThrottleDeceleration);
            Assert.Equal(original.ConnectId, restored.ConnectId);
        }

        [Fact]
        public void ProtocolDisconnect_BoundaryData_RoundTrip()
        {
            var original = new ProtocolDisconnect
            {
                Header = new ProtocolCommandHeader { Command = 0x84, ChannelId = 0xFF },
                Data = 0xFFFFFFFF
            };
            byte[] buf = new byte[8];
            original.Write(buf);
            var restored = ProtocolDisconnect.Read(buf);
            Assert.Equal(original.Data, restored.Data);
        }

        [Fact]
        public void ProtocolSendReliable_BoundaryDataLength_RoundTrip()
        {
            var original = new ProtocolSendReliable
            {
                Header = new ProtocolCommandHeader { Command = 0x86, ChannelId = 0, ReliableSequenceNumber = 0xFFFF },
                DataLength = 0xFFFF
            };
            byte[] buf = new byte[6];
            original.Write(buf);
            var restored = ProtocolSendReliable.Read(buf);
            Assert.Equal(original.Header.ReliableSequenceNumber, restored.Header.ReliableSequenceNumber);
            Assert.Equal(original.DataLength, restored.DataLength);
        }

        [Fact]
        public void ProtocolSendUnreliable_BoundaryValues_RoundTrip()
        {
            var original = new ProtocolSendUnreliable
            {
                Header = new ProtocolCommandHeader { Command = 0x07, ChannelId = 0xFF },
                UnreliableSequenceNumber = 0xFFFF,
                DataLength = 0xFFFF
            };
            byte[] buf = new byte[8];
            original.Write(buf);
            var restored = ProtocolSendUnreliable.Read(buf);
            Assert.Equal(original.UnreliableSequenceNumber, restored.UnreliableSequenceNumber);
            Assert.Equal(original.DataLength, restored.DataLength);
        }

        [Fact]
        public void ProtocolSendFragment_BoundaryValues_RoundTrip()
        {
            var original = new ProtocolSendFragment
            {
                Header = new ProtocolCommandHeader { Command = 0x88, ChannelId = 0xFF },
                StartSequenceNumber = 0xFFFF,
                DataLength = 0xFFFF,
                FragmentCount = 0xFFFFFFFF,
                FragmentNumber = 0xFFFFFFFF,
                TotalLength = 0xFFFFFFFF,
                FragmentOffset = 0xFFFFFFFF
            };
            byte[] buf = new byte[24];
            original.Write(buf);
            var restored = ProtocolSendFragment.Read(buf);
            Assert.Equal(original.StartSequenceNumber, restored.StartSequenceNumber);
            Assert.Equal(original.DataLength, restored.DataLength);
            Assert.Equal(original.FragmentCount, restored.FragmentCount);
            Assert.Equal(original.FragmentNumber, restored.FragmentNumber);
            Assert.Equal(original.TotalLength, restored.TotalLength);
            Assert.Equal(original.FragmentOffset, restored.FragmentOffset);
        }

        // ── Big-endian byte layout verification ───────────────────────────────

        [Fact]
        public void ProtocolCommandHeader_Write_IsBigEndian()
        {
            var h = new ProtocolCommandHeader
            {
                Command = 0x01,
                ChannelId = 0x02,
                ReliableSequenceNumber = 0x0304
            };
            byte[] buf = new byte[4];
            h.Write(buf);

            Assert.Equal(0x01, buf[0]); // Command
            Assert.Equal(0x02, buf[1]); // ChannelId
            Assert.Equal(0x03, buf[2]); // RSN high byte
            Assert.Equal(0x04, buf[3]); // RSN low byte
        }

        [Fact]
        public void ProtocolAcknowledge_Write_IsBigEndian()
        {
            var a = new ProtocolAcknowledge
            {
                Header = new ProtocolCommandHeader { Command = 0x01, ChannelId = 0xFF, ReliableSequenceNumber = 0x0002 },
                ReceivedReliableSequenceNumber = 0x0102,
                ReceivedSentTime = 0x0304
            };
            byte[] buf = new byte[8];
            a.Write(buf);

            // Bytes 4-5: ReceivedReliableSequenceNumber big-endian
            Assert.Equal(0x01, buf[4]);
            Assert.Equal(0x02, buf[5]);
            // Bytes 6-7: ReceivedSentTime big-endian
            Assert.Equal(0x03, buf[6]);
            Assert.Equal(0x04, buf[7]);
        }

        [Fact]
        public void ProtocolBandwidthLimit_Write_IsBigEndian()
        {
            var bl = new ProtocolBandwidthLimit
            {
                Header = new ProtocolCommandHeader { Command = 0x0A },
                IncomingBandwidth = 0x01020304,
                OutgoingBandwidth = 0x05060708
            };
            byte[] buf = new byte[12];
            bl.Write(buf);

            // Bytes 4-7: IncomingBandwidth big-endian
            Assert.Equal(0x01, buf[4]);
            Assert.Equal(0x02, buf[5]);
            Assert.Equal(0x03, buf[6]);
            Assert.Equal(0x04, buf[7]);
            // Bytes 8-11: OutgoingBandwidth big-endian
            Assert.Equal(0x05, buf[8]);
            Assert.Equal(0x06, buf[9]);
            Assert.Equal(0x07, buf[10]);
            Assert.Equal(0x08, buf[11]);
        }

        // ── ENetProtocolCommand enum values ───────────────────────────────────

        [Fact]
        public void ENetProtocolCommand_Acknowledge_Is1()
        {
            Assert.Equal(1, (int)ENetProtocolCommand.Acknowledge);
        }

        [Fact]
        public void ENetProtocolCommand_Connect_Is2()
        {
            Assert.Equal(2, (int)ENetProtocolCommand.Connect);
        }

        [Fact]
        public void ENetProtocolCommand_Mask_Is0x0F()
        {
            Assert.Equal(0x0F, (int)ENetProtocolCommand.Mask);
        }

        [Fact]
        public void ENetProtocolCommand_Count_Is13()
        {
            Assert.Equal(13, (int)ENetProtocolCommand.Count);
        }

        // ── CommandSizes.Sizes array consistency ──────────────────────────────

        [Fact]
        public void CommandSizes_Sizes_LengthEqualsCommandCount()
        {
            Assert.Equal((int)ENetProtocolCommand.Count, CommandSizes.Sizes.Length);
        }

        [Fact]
        public void CommandSizes_GetCommandSize_IgnoresHighBits()
        {
            // Same command with different flag bits should return same size
            byte cmdRaw = (byte)ENetProtocolCommand.Connect;
            byte cmdWithAck = (byte)((byte)ENetProtocolCommand.Connect | (byte)ENetProtocolFlag.CommandFlagAcknowledge);

            Assert.Equal(
                CommandSizes.GetCommandSize(cmdRaw),
                CommandSizes.GetCommandSize(cmdWithAck));
        }
    }
}

