namespace Nethereum.AppChain.Anchoring.Messaging
{
    public class MessageInfo
    {
        public ulong MessageId { get; set; }
        public ulong SourceChainId { get; set; }
        public string Sender { get; set; } = "";
        public ulong TargetChainId { get; set; }
        public string Target { get; set; } = "";
        public byte[] Data { get; set; } = System.Array.Empty<byte>();
        public long Timestamp { get; set; }
    }
}
