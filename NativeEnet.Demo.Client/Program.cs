using System.Text;
using ENet;


Library.Initialize();

for (int i = 0; i < 1000; i++)
{
    Thread.Sleep(100);
    var l = i;
    Task.Factory.StartNew(() => CreateAndSend(l), TaskCreationOptions.LongRunning);
    // Task.Run(() => CreateAndSend(l));
}

while (!Console.KeyAvailable)
{
    Thread.Sleep(1000);
}

Library.Deinitialize();

void CreateAndSend(int index)
{
    using (Host client = new Host()) {
        Address address = new Address();

        address.SetHost("127.0.0.1");
        address.Port = 44556;
        client.Create();

        Peer peer = client.Connect(address, 4);
        var sendTime = DateTime.Now + TimeSpan.FromMilliseconds(1000);

        var data = Enumerable.Range(0, 500).Select(x => (byte)x).ToArray();

        Event netEvent;

        do {

            if(sendTime - DateTime.Now < TimeSpan.Zero)
            {
                Packet packet = default(Packet);

                packet.Create(data, data.Length, PacketFlags.Reliable);
                peer.Send(1, ref packet);
                sendTime = DateTime.Now + TimeSpan.FromMilliseconds(1000);
            }

            bool polled = false;

            while (!polled) {
                if (client.CheckEvents(out netEvent) <= 0) {
                    if (client.Service(15, out netEvent) <= 0)
                        break;

                    polled = true;
                }

                switch (netEvent.Type) {
                    case EventType.None:
                        break;

                    case EventType.Connect:
                        // Console.WriteLine("Client connected to server");
                        Console.WriteLine($"Client connected - ID: {index}, IP: {netEvent.Peer.IP}");
                        break;

                    case EventType.Disconnect:
                        Console.WriteLine("Client disconnected from server");
                        break;

                    case EventType.Timeout:
                        Console.WriteLine("Client connection timeout");
                        break;

                    case EventType.Receive:
                        // Console.WriteLine($"Packet received from server - Channel ID: {netEvent.ChannelID}, Data length: {netEvent.Packet.Length}");
                        netEvent.Packet.Dispose();
                        break;
                }
            }
        } while (true);

        client.Flush();
    }
}