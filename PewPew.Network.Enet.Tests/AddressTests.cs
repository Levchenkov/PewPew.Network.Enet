using System;
using System.Net;
using Xunit;

namespace PewPew.Network.Enet.Tests
{
    public class AddressTests
    {
        // ── SetIP ─────────────────────────────────────────────────────────────

        [Fact]
        public void SetIP_WithValidIPv4_ReturnsTrue()
        {
            var addr = new Address();
            Assert.True(addr.SetIP("192.168.1.1"));
        }

        [Fact]
        public void SetIP_WithValidIPv6_ReturnsTrue()
        {
            var addr = new Address();
            Assert.True(addr.SetIP("::1"));
        }

        [Fact]
        public void SetIP_WithWildcard_ReturnsTrue()
        {
            var addr = new Address();
            Assert.True(addr.SetIP("*"));
        }

        [Fact]
        public void SetIP_WithInvalidString_ReturnsFalse()
        {
            var addr = new Address();
            Assert.False(addr.SetIP("not-an-ip"));
        }

        [Fact]
        public void SetIP_WithNull_ThrowsArgumentNullException()
        {
            var addr = new Address();
            Assert.Throws<ArgumentNullException>(() => addr.SetIP(null!));
        }

        // ── GetIP ─────────────────────────────────────────────────────────────

        [Fact]
        public void GetIP_AfterSetIPv4_ReturnsIPv4DottedDecimal()
        {
            var addr = new Address();
            addr.SetIP("10.0.0.1");
            Assert.Equal("10.0.0.1", addr.GetIP());
        }

        [Fact]
        public void GetIP_AfterSetIPv6_ReturnsIPv6()
        {
            var addr = new Address();
            addr.SetIP("::1");
            Assert.Equal("::1", addr.GetIP());
        }

        [Fact]
        public void GetIP_WhenNotSet_ReturnsEmptyString()
        {
            var addr = new Address();
            Assert.Equal(string.Empty, addr.GetIP());
        }

        [Fact]
        public void IP_Property_WhenNotSet_ReturnsEmptyString()
        {
            var addr = new Address();
            Assert.Equal(string.Empty, addr.IP);
        }

        // ── Port ──────────────────────────────────────────────────────────────

        [Fact]
        public void Port_GetSetRoundTrip()
        {
            var addr = new Address();
            addr.Port = 7777;
            Assert.Equal(7777, addr.Port);
        }

        [Fact]
        public void Port_DefaultIsZero()
        {
            var addr = new Address();
            Assert.Equal(0, addr.Port);
        }

        // ── ToString ──────────────────────────────────────────────────────────

        [Fact]
        public void ToString_ReturnsIPAndPort()
        {
            var addr = new Address();
            addr.SetIP("127.0.0.1");
            addr.Port = 7777;
            Assert.Equal("127.0.0.1:7777", addr.ToString());
        }

        // ── Equals / GetHashCode / operators ─────────────────────────────────

        [Fact]
        public void Equals_SameIPAndPort_ReturnsTrue()
        {
            var a = new Address();
            a.SetIP("127.0.0.1");
            a.Port = 7777;

            var b = new Address();
            b.SetIP("127.0.0.1");
            b.Port = 7777;

            Assert.True(a.Equals(b));
            Assert.True(a == b);
            Assert.False(a != b);
        }

        [Fact]
        public void Equals_DifferentPort_ReturnsFalse()
        {
            var a = new Address();
            a.SetIP("127.0.0.1");
            a.Port = 7777;

            var b = new Address();
            b.SetIP("127.0.0.1");
            b.Port = 9999;

            Assert.False(a.Equals(b));
            Assert.True(a != b);
        }

        [Fact]
        public void Equals_DifferentIP_ReturnsFalse()
        {
            var a = new Address();
            a.SetIP("127.0.0.1");
            a.Port = 7777;

            var b = new Address();
            b.SetIP("127.0.0.2");
            b.Port = 7777;

            Assert.False(a.Equals(b));
        }

        [Fact]
        public void GetHashCode_SameValues_ReturnsSameHash()
        {
            var a = new Address();
            a.SetIP("10.0.0.1");
            a.Port = 1234;

            var b = new Address();
            b.SetIP("10.0.0.1");
            b.Port = 1234;

            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Equals_NonAddressObject_ReturnsFalse()
        {
            var addr = new Address();
            addr.SetIP("127.0.0.1");
            addr.Port = 7777;

            Assert.False(addr.Equals("127.0.0.1:7777"));
        }

        // ── FromEndPoint / ToEndPoint ─────────────────────────────────────────

        [Fact]
        public void FromEndPoint_ToEndPoint_RoundTrip()
        {
            var original = new IPEndPoint(IPAddress.Loopback, 4321);
            var addr = Address.FromEndPoint(original);
            var restored = addr.ToEndPoint();

            Assert.Equal(original.Port, restored.Port);
            // IPv4 loopback may be stored as IPv4-mapped IPv6
            Assert.True(
                IPAddress.IsLoopback(restored.Address) ||
                restored.Address.Equals(original.Address.MapToIPv6()));
        }

        [Fact]
        public void InternalAddress_WhenNotSet_ReturnsIPv6Any()
        {
            var addr = new Address();
            Assert.Equal(IPAddress.IPv6Any, addr.InternalAddress);
        }

        // ── SetHost ───────────────────────────────────────────────────────────

        [Fact]
        public void SetHost_WithNull_ThrowsArgumentNullException()
        {
            var addr = new Address();
            Assert.Throws<ArgumentNullException>(() => addr.SetHost(null!));
        }

        [Fact]
        public void SetHost_WithValidIPAddress_ReturnsTrue()
        {
            var addr = new Address();
            Assert.True(addr.SetHost("127.0.0.1"));
        }
    }
}
