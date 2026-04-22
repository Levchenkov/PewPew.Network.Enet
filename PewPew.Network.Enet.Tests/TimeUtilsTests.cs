using System.Threading;
using Xunit;
using PewPew.Network.Enet.Internal;

namespace PewPew.Network.Enet.Tests
{
    public class TimeUtilsTests
    {
        // ── GetTime ───────────────────────────────────────────────────────────

        [Fact]
        public void GetTime_ReturnsNonZero()
        {
            uint t = TimeUtils.GetTime();
            Assert.True(t > 0);
        }

        [Fact]
        public void GetTime_IsMonotonicallyNonDecreasing()
        {
            uint t1 = TimeUtils.GetTime();
            uint t2 = TimeUtils.GetTime();
            // t2 >= t1 unless we've wrapped the 32-bit counter (very unlikely in a test)
            Assert.True(t2 >= t1);
        }

        [Fact]
        public void GetTime_AfterSleep_IncreasesOrWraps()
        {
            uint t1 = TimeUtils.GetTime();
            Thread.Sleep(5);
            uint t2 = TimeUtils.GetTime();
            Assert.True(t2 >= t1);
        }

        // ── TimeLess ─────────────────────────────────────────────────────────

        [Fact]
        public void TimeLess_WhenALessThanB_ReturnsTrue()
        {
            Assert.True(TimeUtils.TimeLess(100, 200));
        }

        [Fact]
        public void TimeLess_WhenAGreaterThanB_ReturnsFalse()
        {
            Assert.False(TimeUtils.TimeLess(200, 100));
        }

        [Fact]
        public void TimeLess_WhenEqual_ReturnsFalse()
        {
            Assert.False(TimeUtils.TimeLess(100, 100));
        }

        // ── TimeGreater ───────────────────────────────────────────────────────

        [Fact]
        public void TimeGreater_WhenAGreaterThanB_ReturnsTrue()
        {
            Assert.True(TimeUtils.TimeGreater(200, 100));
        }

        [Fact]
        public void TimeGreater_WhenALessThanB_ReturnsFalse()
        {
            Assert.False(TimeUtils.TimeGreater(100, 200));
        }

        [Fact]
        public void TimeGreater_WhenEqual_ReturnsFalse()
        {
            Assert.False(TimeUtils.TimeGreater(100, 100));
        }

        // ── TimeLessEqual ─────────────────────────────────────────────────────

        [Fact]
        public void TimeLessEqual_WhenALessThanB_ReturnsTrue()
        {
            Assert.True(TimeUtils.TimeLessEqual(100, 200));
        }

        [Fact]
        public void TimeLessEqual_WhenEqual_ReturnsTrue()
        {
            Assert.True(TimeUtils.TimeLessEqual(100, 100));
        }

        [Fact]
        public void TimeLessEqual_WhenAGreaterThanB_ReturnsFalse()
        {
            Assert.False(TimeUtils.TimeLessEqual(200, 100));
        }

        // ── TimeGreaterEqual ──────────────────────────────────────────────────

        [Fact]
        public void TimeGreaterEqual_WhenAGreaterThanB_ReturnsTrue()
        {
            Assert.True(TimeUtils.TimeGreaterEqual(200, 100));
        }

        [Fact]
        public void TimeGreaterEqual_WhenEqual_ReturnsTrue()
        {
            Assert.True(TimeUtils.TimeGreaterEqual(100, 100));
        }

        [Fact]
        public void TimeGreaterEqual_WhenALessThanB_ReturnsFalse()
        {
            Assert.False(TimeUtils.TimeGreaterEqual(100, 200));
        }

        // ── TimeDifference ────────────────────────────────────────────────────

        [Fact]
        public void TimeDifference_NormalCase_ReturnsAbsDifference()
        {
            Assert.Equal(50u, TimeUtils.TimeDifference(150, 100));
            Assert.Equal(50u, TimeUtils.TimeDifference(100, 150));
        }

        [Fact]
        public void TimeDifference_Equal_ReturnsZero()
        {
            Assert.Equal(0u, TimeUtils.TimeDifference(100, 100));
        }

        // ── Overflow wrap ─────────────────────────────────────────────────────

        [Fact]
        public void TimeOverflow_ConstantIsExpected()
        {
            Assert.Equal(86_400_000u, TimeUtils.TimeOverflow);
        }

        [Fact]
        public void TimeLess_NearOverflowBoundary_UsesSentinelLogic()
        {
            // Near wrap: a = TimeOverflow - 1, b = TimeOverflow + 1 (just wrapped)
            uint a = TimeUtils.TimeOverflow - 1;
            uint b = TimeUtils.TimeOverflow + 1;
            // b - a = 2, which is < TimeOverflow, so b > a (TimeGreater returns true)
            Assert.True(TimeUtils.TimeGreater(b, a));
        }
    }
}
