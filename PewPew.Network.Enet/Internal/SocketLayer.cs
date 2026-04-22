using System;
using System.Net;
using System.Net.Sockets;

namespace PewPew.Network.Enet.Internal
{
    /// <summary>
    /// UDP socket abstraction. Uses IPv6 dual-stack to support both IPv4 and IPv6,
    /// matching the C ENet behaviour with in6_addr.
    /// </summary>
    internal sealed class UdpSocket : IUdpSocket
    {
        private Socket? _socket;
        private bool _disposed;
        // Reused across ReceiveFrom calls to avoid allocating a new IPEndPoint per datagram
        private IPEndPoint _receiveEndPoint = new IPEndPoint(IPAddress.IPv6Any, 0);

        public bool IsValid => _socket != null && !_disposed;

        /// <summary>
        /// Create and bind a UDP socket. Pass <c>null</c> address to bind any.
        /// </summary>
        public void Create(Address? bindAddress, int receiveBufferSize, int sendBufferSize)
        {
            _socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);

            // Enable IPv6 dual-stack (maps IPv4 connections to IPv6)
            _socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            _socket.Blocking = false;

            if (receiveBufferSize > 0)
                _socket.ReceiveBufferSize = receiveBufferSize;
            if (sendBufferSize > 0)
                _socket.SendBufferSize = sendBufferSize;

            IPEndPoint ep;
            if (bindAddress.HasValue)
            {
                ep = bindAddress.Value.ToEndPoint();
                // Normalize IPv4-mapped for binding
                if (ep.Address.IsIPv4MappedToIPv6)
                    ep = new IPEndPoint(ep.Address, ep.Port);
            }
            else
            {
                ep = new IPEndPoint(IPAddress.IPv6Any, 0);
            }

            _socket.Bind(ep);
            SetNonBlocking();
        }

        public Address GetLocalAddress()
        {
            if (_socket?.LocalEndPoint is IPEndPoint ep)
                return Address.FromEndPoint(ep);
            return default;
        }

        private void SetNonBlocking()
        {
            if (_socket == null) return;
            _socket.Blocking = false;
        }

        public void SetOption(SocketOptionName option, int value)
        {
            _socket?.SetSocketOption(SocketOptionLevel.Socket, option, value);
        }

        public void SetBroadcast(bool enable)
        {
            _socket?.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, enable);
        }

        public void SetNoDelay(bool enable)
        {
            _socket?.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, enable);
        }

        /// <summary>
        /// Send data to the specified remote endpoint.
        /// Returns number of bytes sent, or -1 on error.
        /// </summary>
        public int SendTo(byte[] data, int offset, int length, IPEndPoint remote)
        {
            if (_socket == null) return -1;
            try
            {
                return _socket.SendTo(data, offset, length, SocketFlags.None, remote);
            }
            catch (SocketException)
            {
                return -1;
            }
        }

        /// <summary>
        /// Receive a datagram. Returns number of bytes received, or -1 if nothing available.
        /// </summary>
        public int ReceiveFrom(byte[] buffer, int offset, int length, out IPEndPoint? remote)
        {
            remote = null;
            if (_socket == null) return -1;

            try
            {
                // Pass the reused endpoint; .NET only allocates a new IPEndPoint if the
                // sender address differs from the previous call, so consecutive packets
                // from the same peer incur zero allocations.
                EndPoint ep = _receiveEndPoint;
                int received = _socket.ReceiveFrom(buffer, offset, length, SocketFlags.None, ref ep);
                // Update the cached reference (no-op if address unchanged)
                _receiveEndPoint = (IPEndPoint)ep;
                remote = _receiveEndPoint;
                return received;
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock ||
                                              ex.SocketErrorCode == SocketError.TryAgain ||
                                              ex.SocketErrorCode == SocketError.NoData)
            {
                return 0; // nothing to receive
            }
            catch (SocketException)
            {
                return -1;
            }
        }

        /// <summary>
        /// Wait for socket readability/writability. Returns OR of ENetSocketWait flags.
        /// </summary>
        public int Wait(ref uint condition, ulong timeoutUs)
        {
            if (_socket == null) return -1;

            bool wantSend = (condition & 1) != 0;
            bool wantRecv = (condition & 2) != 0;

            int timeoutMs = timeoutUs == uint.MaxValue ? -1 : (int)(timeoutUs / 1000);

            condition = 0;
            try
            {
                if (wantRecv && _socket.Poll(timeoutMs * 1000, SelectMode.SelectRead))
                    condition |= 2;
                if (wantSend && _socket.Poll(0, SelectMode.SelectWrite))
                    condition |= 1;
                return 0;
            }
            catch (SocketException)
            {
                return -1;
            }
        }

        /// <summary>
        /// Quick check: any data available to read?
        /// </summary>
        public bool DataAvailable =>
            _socket?.Available > 0;

        public void Dispose()
        {
            if (!_disposed)
            {
                _socket?.Close();
                _socket?.Dispose();
                _socket = null;
                _disposed = true;
            }
        }
    }
}
