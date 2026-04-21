using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PewPew.Network.Enet
{
    /// <summary>
    /// Represents an ENet host address (IP + port).
    /// Supports IPv4 and IPv6 (IPv4 is stored as IPv4-mapped IPv6, matching the C implementation).
    /// </summary>
    public struct Address
    {
        private IPAddress _ip;
        private ushort _port;

        public ushort Port
        {
            get => _port;
            set => _port = value;
        }

        public string IP => _ip?.ToString() ?? string.Empty;

        internal IPEndPoint ToEndPoint() =>
            new IPEndPoint(_ip ?? IPAddress.IPv6Any, _port);

        internal static Address FromEndPoint(IPEndPoint ep)
        {
            var a = new Address();
            a._ip = ep.Address;
            a._port = (ushort)ep.Port;
            return a;
        }

        internal IPAddress InternalAddress => _ip ?? IPAddress.IPv6Any;

        public bool SetIP(string ip)
        {
            if (ip == null) throw new ArgumentNullException(nameof(ip));

            if (ip == "*")
            {
                _ip = IPAddress.IPv6Any;
                return true;
            }

            if (!IPAddress.TryParse(ip, out IPAddress? addr))
                return false;

            // Normalize IPv4 to IPv4-mapped IPv6 (matching C ENet in6addr_any / ipv4 union)
            if (addr.AddressFamily == AddressFamily.InterNetwork)
                addr = addr.MapToIPv6();

            _ip = addr;
            return true;
        }

        public bool SetHost(string hostName)
        {
            if (hostName == null) throw new ArgumentNullException(nameof(hostName));

            // Try as IP first
            if (SetIP(hostName)) return true;

            try
            {
                var addresses = Dns.GetHostAddresses(hostName);
                foreach (var addr in addresses)
                {
                    if (addr.AddressFamily == AddressFamily.InterNetworkV6 ||
                        addr.AddressFamily == AddressFamily.InterNetwork)
                    {
                        _ip = addr.AddressFamily == AddressFamily.InterNetwork
                            ? addr.MapToIPv6()
                            : addr;
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public string GetIP()
        {
            if (_ip == null) return string.Empty;
            // If IPv4-mapped IPv6, return the IPv4 form
            if (_ip.IsIPv4MappedToIPv6)
                return _ip.MapToIPv4().ToString();
            return _ip.ToString();
        }

        public string GetHost()
        {
            if (_ip == null) return string.Empty;
            try
            {
                var ep = new IPEndPoint(_ip.IsIPv4MappedToIPv6 ? _ip.MapToIPv4() : _ip, _port);
                var entry = Dns.GetHostEntry(ep.Address);
                return entry.HostName;
            }
            catch
            {
                return GetIP();
            }
        }

        public override string ToString() => $"{GetIP()}:{_port}";

        public override bool Equals(object? obj)
        {
            if (obj is Address other)
                return _port == other._port && Equals(_ip, other._ip);
            return false;
        }

        public override int GetHashCode() => HashCode.Combine(_ip, _port);

        public static bool operator ==(Address a, Address b) => a.Equals(b);
        public static bool operator !=(Address a, Address b) => !a.Equals(b);
    }
}
