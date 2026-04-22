using System.Collections.Generic;
using System.Net;
using PewPew.Network.Enet;
using PewPew.Network.Enet.Internal;

namespace PewPew.Network.Enet.Tests.Fakes
{
    /// <summary>
    /// In-memory UDP socket for unit tests. Two paired instances route packets between
    /// each other without any real network I/O. SendTo on one delivers to the other's
    /// ReceiveFrom, with the sender's local endpoint reported as the remote address.
    /// </summary>
    internal sealed class FakeUdpSocket : IUdpSocket
    {
        private readonly Queue<(byte[] Data, IPEndPoint Sender)> _receiveQueue = new();
        private IPEndPoint? _localEndPoint;
        private FakeUdpSocket? _partner;

        public bool IsValid => true;

        public void Create(Address? bindAddress, int receiveBufferSize, int sendBufferSize)
        {
            // If a bind address is given and we have no local endpoint yet, store it.
            if (_localEndPoint == null && bindAddress.HasValue)
                _localEndPoint = bindAddress.Value.ToEndPoint();
        }

        public Address GetLocalAddress() =>
            _localEndPoint != null ? Address.FromEndPoint(_localEndPoint) : default;

        public int SendTo(byte[] data, int offset, int length, IPEndPoint remote)
        {
            if (_partner == null) return -1;
            var copy = new byte[length];
            System.Buffer.BlockCopy(data, offset, copy, 0, length);
            _partner.Deliver(copy, _localEndPoint ?? new IPEndPoint(System.Net.IPAddress.Loopback, 0));
            return length;
        }

        public int ReceiveFrom(byte[] buffer, int offset, int length, out IPEndPoint? remote)
        {
            if (!_receiveQueue.TryDequeue(out var item))
            {
                remote = null;
                return 0;
            }

            int len = System.Math.Min(item.Data.Length, length);
            System.Buffer.BlockCopy(item.Data, 0, buffer, offset, len);
            remote = item.Sender;
            return len;
        }

        public int Wait(ref uint condition, ulong timeoutUs)
        {
            bool hasData = _receiveQueue.Count > 0;
            condition = hasData && (condition & 2) != 0 ? 2u : 0u;
            return 0;
        }

        public void Dispose() { }

        internal void Deliver(byte[] data, IPEndPoint sender) =>
            _receiveQueue.Enqueue((data, sender));

        /// <summary>
        /// Creates two paired sockets. Packets sent from one are delivered to the other.
        /// </summary>
        public static (FakeUdpSocket Client, FakeUdpSocket Server) CreatePair(
            IPEndPoint clientEndPoint, IPEndPoint serverEndPoint)
        {
            var client = new FakeUdpSocket { _localEndPoint = clientEndPoint };
            var server = new FakeUdpSocket { _localEndPoint = serverEndPoint };
            client._partner = server;
            server._partner = client;
            return (client, server);
        }
    }
}
