using System.Collections.Generic;

namespace PewPew.Network.Enet.Internal
{
    /// <summary>
    /// Object pools for the three hot-path command types.
    /// Eliminates per-packet heap allocations for ENetOutgoingCommand,
    /// ENetAcknowledgement, and ENetIncomingCommand.
    /// </summary>
    internal sealed class ENetCommandPool
    {
        private const int MaxPoolSize = 512;

        private readonly Stack<ENetOutgoingCommand>   _outgoing  = new(64);
        private readonly Stack<ENetAcknowledgement>   _acks      = new(64);
        private readonly Stack<ENetIncomingCommand>   _incoming  = new(64);

        // ── Outgoing ─────────────────────────────────────────────────────────

        public ENetOutgoingCommand GetOutgoing()
            => _outgoing.TryPop(out var item) ? item : new ENetOutgoingCommand();

        public void ReturnOutgoing(ENetOutgoingCommand cmd)
        {
            if (_outgoing.Count >= MaxPoolSize) return;
            cmd.ReliableSequenceNumber = 0;
            cmd.UnreliableSequenceNumber = 0;
            cmd.SentTime = 0;
            cmd.RoundTripTimeout = 0;
            cmd.RoundTripTimeoutLimit = 0;
            cmd.FragmentOffset = 0;
            cmd.FragmentLength = 0;
            cmd.SendAttempts = 0;
            cmd.Packet = null;
            cmd.Command = default;
            _outgoing.Push(cmd);
        }

        // ── Acknowledgement ───────────────────────────────────────────────────

        public ENetAcknowledgement GetAck()
            => _acks.TryPop(out var item) ? item : new ENetAcknowledgement();

        public void ReturnAck(ENetAcknowledgement ack)
        {
            if (_acks.Count >= MaxPoolSize) return;
            ack.SentTime = 0;
            ack.Command = default;
            _acks.Push(ack);
        }

        // ── Incoming ──────────────────────────────────────────────────────────

        public ENetIncomingCommand GetIncoming()
            => _incoming.TryPop(out var item) ? item : new ENetIncomingCommand();

        public void ReturnIncoming(ENetIncomingCommand cmd)
        {
            if (_incoming.Count >= MaxPoolSize) return;
            cmd.ReliableSequenceNumber = 0;
            cmd.UnreliableSequenceNumber = 0;
            cmd.FragmentCount = 0;
            cmd.FragmentsRemaining = 0;
            cmd.Fragments = null;
            cmd.Packet = null;
            cmd.Command = default;
            _incoming.Push(cmd);
        }
    }
}
