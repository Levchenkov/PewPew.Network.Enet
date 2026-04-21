namespace PewPew.Network.Enet.Internal
{
    /// <summary>
    /// Pending acknowledgement queued for sending to the remote peer.
    /// Matches ENetAcknowledgement in C.
    /// </summary>
    internal class ENetAcknowledgement
    {
        // Intrusive list node for ENetPeer.acknowledgements list
        public readonly ENetListNode<ENetAcknowledgement> ListNode;
        public uint SentTime;
        public ENetProtocol Command;

        public ENetAcknowledgement()
        {
            ListNode = new ENetListNode<ENetAcknowledgement> { Owner = this };
        }
    }

    /// <summary>
    /// An outgoing command (reliable or unreliable) queued for transmission or awaiting ACK.
    /// Matches ENetOutgoingCommand in C.
    /// </summary>
    internal class ENetOutgoingCommand
    {
        public readonly ENetListNode<ENetOutgoingCommand> ListNode;

        public ushort ReliableSequenceNumber;
        public ushort UnreliableSequenceNumber;
        public uint SentTime;
        public uint RoundTripTimeout;
        public uint RoundTripTimeoutLimit;
        public uint FragmentOffset;
        public ushort FragmentLength;
        public ushort SendAttempts;
        public ENetProtocol Command;
        public ENetPacket? Packet;

        public ENetOutgoingCommand()
        {
            ListNode = new ENetListNode<ENetOutgoingCommand> { Owner = this };
        }
    }

    /// <summary>
    /// An incoming command received from the remote peer, pending assembly/dispatch.
    /// Matches ENetIncomingCommand in C.
    /// </summary>
    internal class ENetIncomingCommand
    {
        public readonly ENetListNode<ENetIncomingCommand> ListNode;

        public ushort ReliableSequenceNumber;
        public ushort UnreliableSequenceNumber;
        public ENetProtocol Command;
        public uint FragmentCount;
        public uint FragmentsRemaining;
        /// <summary>Bit-field tracking which fragments have been received.</summary>
        public uint[]? Fragments;
        public ENetPacket? Packet;

        public ENetIncomingCommand()
        {
            ListNode = new ENetListNode<ENetIncomingCommand> { Owner = this };
        }
    }
}
