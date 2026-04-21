using System;
using System.Diagnostics;
using System.Threading;

namespace PewPew.Network.Enet.Internal
{
    internal static class TimeUtils
    {
        // ENet wraps time at 24 hours of milliseconds
        public const uint TimeOverflow = 86_400_000;

        private static long _startTimestamp;
        private static int _initialized;

        public static uint GetTime()
        {
            long freq = Stopwatch.Frequency;
            long now = Stopwatch.GetTimestamp();

            // Lazy-initialize start timestamp (atomic, like ENET_ATOMIC_CAS pattern)
            if (_initialized == 0)
            {
                // start 1ms before current to avoid zero
                long wantStart = now - (freq / 1000);
                long prev = Interlocked.CompareExchange(ref _startTimestamp, wantStart, 0L);
                if (prev == 0L)
                    _startTimestamp = wantStart;
                Interlocked.Exchange(ref _initialized, 1);
            }

            long elapsedTicks = now - Volatile.Read(ref _startTimestamp);
            // Convert ticks → milliseconds
            long ms = elapsedTicks * 1000 / freq;
            return (uint)(ms & 0xFFFF_FFFF);
        }

        public static bool TimeLess(uint a, uint b) => (a - b) >= TimeOverflow;
        public static bool TimeGreater(uint a, uint b) => (b - a) >= TimeOverflow;
        public static bool TimeLessEqual(uint a, uint b) => !TimeGreater(a, b);
        public static bool TimeGreaterEqual(uint a, uint b) => !TimeLess(a, b);

        public static uint TimeDifference(uint a, uint b) =>
            (a - b) >= TimeOverflow ? b - a : a - b;
    }
}
