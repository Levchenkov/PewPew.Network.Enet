using PewPew.Network.Enet;

Library.Initialize();

using (Host server = new Host()) {
    Address address = new Address();

    address.Port = 44556;
    server.Create(address, 1000, 4);

    var buffer = new byte[5000];

    Event netEvent;

    while (!Console.KeyAvailable) {
        bool polled = false;

        while (!polled) {
            if (server.CheckEvents(out netEvent) <= 0) {
                if (server.Service(15, out netEvent) <= 0)
                    break;

                polled = true;
            }

            switch (netEvent.Type) {
                case EventType.None:
                    break;

                case EventType.Connect:
                    Console.WriteLine($"Client connected - ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}");
                    break;

                case EventType.Disconnect:
                    Console.WriteLine($"Client disconnected - ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}");
                    break;

                case EventType.Timeout:
                    Console.WriteLine($"Client timeout - ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}");
                    break;

                case EventType.Receive:
                    // Console.WriteLine($"Packet received from peer ID: {netEvent.Peer.ID}, IP: {netEvent.Peer.IP}, Channel ID: {netEvent.ChannelID}, Data length: {netEvent.Packet.Length}");

                    var packetLength = netEvent.Packet.Length;

                    netEvent.Packet.CopyTo(buffer);
                    netEvent.Packet.Dispose();

                    Packet packet = default(Packet);
                    byte[] data = buffer;

                    packet.Create(data, packetLength, PacketFlags.Reliable);
                    netEvent.Peer.Send(2, ref packet);
                    break;
            }
        }
    }

    server.Flush();
}
Library.Deinitialize();