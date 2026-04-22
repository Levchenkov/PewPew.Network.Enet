using System;
using System.Net;

namespace PewPew.Network.Enet.Internal
{
    internal interface IUdpSocket : IDisposable
    {
        bool IsValid { get; }
        void Create(Address? bindAddress, int receiveBufferSize, int sendBufferSize);
        Address GetLocalAddress();
        int SendTo(byte[] data, int offset, int length, IPEndPoint remote);
        int ReceiveFrom(byte[] buffer, int offset, int length, out IPEndPoint? remote);
        int Wait(ref uint condition, ulong timeoutUs);
    }
}
