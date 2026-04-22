using Xunit;

namespace PewPew.Network.Enet.Tests
{
    public class LibraryTests
    {
        [Fact]
        public void Version_MatchesComputedFormula()
        {
            uint expected = (uint)((Library.VersionMajor << 16) | (Library.VersionMinor << 8) | Library.VersionPatch);
            Assert.Equal(expected, Library.Version);
        }

        [Fact]
        public void VersionMajor_Is2()
        {
            Assert.Equal(2, Library.VersionMajor);
        }

        [Fact]
        public void VersionMinor_Is4()
        {
            Assert.Equal(4, Library.VersionMinor);
        }

        [Fact]
        public void MaxPeers_Is4095()
        {
            Assert.Equal(0xFFF, Library.MaxPeers);
        }

        [Fact]
        public void MaxChannelCount_Is255()
        {
            Assert.Equal(255, Library.MaxChannelCount);
        }

        [Fact]
        public void Initialize_ReturnsTrue()
        {
            Assert.True(Library.Initialize());
        }

        [Fact]
        public void Deinitialize_DoesNotThrow()
        {
            var ex = Record.Exception(() => Library.Deinitialize());
            Assert.Null(ex);
        }

        [Fact]
        public void Time_IsNonZero()
        {
            uint t = Library.Time;
            Assert.True(t > 0);
        }

        [Fact]
        public void Time_IsMonotonicallyNonDecreasing()
        {
            uint t1 = Library.Time;
            uint t2 = Library.Time;
            Assert.True(t2 >= t1 || (t1 - t2) > Internal.TimeUtils.TimeOverflow / 2);
        }
    }
}
