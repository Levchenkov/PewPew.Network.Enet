using Xunit;
using PewPew.Network.Enet.Internal;

namespace PewPew.Network.Enet.Tests
{
    /// <summary>
    /// Tests for <see cref="ENetPeer.Throttle"/>.
    ///
    /// The C# implementation's first branch is:
    ///   if (LastRoundTripTime &lt;= LastRoundTripTime + LastRoundTripTimeVariance)
    /// Since LastRoundTripTimeVariance is uint (non-negative) this condition is always true
    /// unless overflow occurs, meaning the function always sets PacketThrottle = PacketThrottleLimit
    /// and returns 1 in normal usage.  Tests capture this actual behaviour to guard regressions.
    /// </summary>
    public class PeerThrottleTests
    {
        private static ENetPeer MakePeer(
            uint lastRtt = 100,
            uint lastRttVariance = 10,
            uint throttleLimit = 32,
            uint throttleAcceleration = 2,
            uint throttleDeceleration = 2)
        {
            var peer = new ENetPeer
            {
                LastRoundTripTime = lastRtt,
                LastRoundTripTimeVariance = lastRttVariance,
                PacketThrottle = 16,
                PacketThrottleLimit = throttleLimit,
                PacketThrottleAcceleration = throttleAcceleration,
                PacketThrottleDeceleration = throttleDeceleration
            };
            return peer;
        }

        // ── First-branch always-true: returns 1, sets PacketThrottle = Limit ──

        [Fact]
        public void Throttle_WithNonZeroVariance_Returns1()
        {
            var peer = MakePeer(lastRtt: 100, lastRttVariance: 10);
            int result = peer.Throttle(100);
            Assert.Equal(1, result);
        }

        [Fact]
        public void Throttle_SetsPacketThrottleToLimit()
        {
            var peer = MakePeer(lastRtt: 100, lastRttVariance: 10, throttleLimit: 32);
            peer.PacketThrottle = 0; // start at 0
            peer.Throttle(200);
            Assert.Equal(32u, peer.PacketThrottle);
        }

        [Fact]
        public void Throttle_WithZeroVariance_FirstBranchStillTrue()
        {
            // 100 <= 100 + 0  → true
            var peer = MakePeer(lastRtt: 100, lastRttVariance: 0, throttleLimit: 32);
            int result = peer.Throttle(999);
            Assert.Equal(1, result);
            Assert.Equal(32u, peer.PacketThrottle);
        }

        [Fact]
        public void Throttle_WithZeroRtt_Returns1()
        {
            var peer = MakePeer(lastRtt: 0, lastRttVariance: 0, throttleLimit: 32);
            int result = peer.Throttle(0);
            Assert.Equal(1, result);
        }

        [Fact]
        public void Throttle_PacketThrottleLimit_IsRespected()
        {
            var peer = MakePeer(lastRtt: 100, lastRttVariance: 5, throttleLimit: 20);
            peer.Throttle(100);
            Assert.Equal(peer.PacketThrottleLimit, peer.PacketThrottle);
        }

        [Fact]
        public void Throttle_DoesNotSetPacketThrottleAboveLimit()
        {
            var peer = MakePeer(lastRtt: 100, lastRttVariance: 10, throttleLimit: 32);
            peer.PacketThrottle = 50; // artificially above limit
            peer.Throttle(100);
            Assert.Equal(32u, peer.PacketThrottle); // clamped to limit
        }

        [Fact]
        public void Throttle_MultipleConsecutiveCalls_AlwaysReturns1()
        {
            var peer = MakePeer(lastRtt: 200, lastRttVariance: 50, throttleLimit: 32);
            for (int i = 0; i < 10; i++)
            {
                int r = peer.Throttle((uint)(50 + i * 20));
                Assert.Equal(1, r);
            }
        }

        [Fact]
        public void Throttle_PacketThrottleLimit_Zero_SetsThrottleToZero()
        {
            var peer = MakePeer(lastRtt: 100, lastRttVariance: 1, throttleLimit: 0);
            peer.Throttle(100);
            Assert.Equal(0u, peer.PacketThrottle);
        }
    }
}
