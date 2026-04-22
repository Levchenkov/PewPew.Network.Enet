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
    }
}
