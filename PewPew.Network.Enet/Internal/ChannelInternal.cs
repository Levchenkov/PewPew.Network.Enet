namespace PewPew.Network.Enet.Internal
{
    /// <summary>
    /// Per-channel state for a peer. Matches ENetChannel in C.
    /// </summary>
    internal class ENetChannel
    {
        public ushort OutgoingReliableSequenceNumber;
        public ushort OutgoingUnreliableSequenceNumber;
        public ushort UsedReliableWindows;
        public ushort[] ReliableWindows = new ushort[ProtocolConstants.PeerReliableWindows];
        public ushort IncomingReliableSequenceNumber;
        public ushort IncomingUnreliableSequenceNumber;

        public readonly ENetList<ENetIncomingCommand> IncomingReliableCommands = new();
        public readonly ENetList<ENetIncomingCommand> IncomingUnreliableCommands = new();

        public void Reset()
        {
            OutgoingReliableSequenceNumber = 0;
            OutgoingUnreliableSequenceNumber = 0;
            UsedReliableWindows = 0;
            IncomingReliableSequenceNumber = 0;
            IncomingUnreliableSequenceNumber = 0;
            for (int i = 0; i < ReliableWindows.Length; i++)
                ReliableWindows[i] = 0;
            IncomingReliableCommands.Clear();
            IncomingUnreliableCommands.Clear();
        }
    }
}
