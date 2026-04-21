namespace PewPew.Network.Enet
{
    public static class Library
    {
        public const int VersionMajor = 2;
        public const int VersionMinor = 4;
        public const int VersionPatch = 9;

        public static uint Version =>
            (uint)((VersionMajor << 16) | (VersionMinor << 8) | VersionPatch);

        public const int MaxPeers = 0xFFF; // 4095
        public const int MaxChannelCount = 255;

        /// <summary>
        /// Initialize the ENet library. No-op in managed implementation
        /// (retained for API compatibility with ENet-CSharp-SoftwareGuy).
        /// </summary>
        public static bool Initialize() => true;

        /// <summary>
        /// Deinitialize the ENet library. No-op in managed implementation.
        /// </summary>
        public static void Deinitialize() { }

        /// <summary>Returns a monotonic timestamp in milliseconds.</summary>
        public static uint Time => Internal.TimeUtils.GetTime();
    }
}
