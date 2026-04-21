namespace PewPew.Network.Enet
{
    public struct Event
    {
        internal Internal.ENetEvent? NativeEvent;

        internal Event(Internal.ENetEvent e)
        {
            NativeEvent = e;
        }

        public EventType Type => NativeEvent != null
            ? MapEventType(NativeEvent.Type)
            : EventType.None;

        public Peer Peer => NativeEvent?.Peer != null
            ? new Peer(NativeEvent.Peer)
            : default;

        public byte ChannelID => NativeEvent?.ChannelId ?? 0;

        public uint Data => NativeEvent?.Data ?? 0;

        public Packet Packet => NativeEvent?.Packet != null
            ? new Packet(NativeEvent.Packet)
            : default;

        private static EventType MapEventType(Internal.ENetEventType t) => t switch
        {
            Internal.ENetEventType.Connect => EventType.Connect,
            Internal.ENetEventType.Disconnect => EventType.Disconnect,
            Internal.ENetEventType.Receive => EventType.Receive,
            Internal.ENetEventType.DisconnectTimeout => EventType.Timeout,
            _ => EventType.None
        };
    }
}
