namespace Nethereum.AppChain.Anchoring.Hub
{
    public class HubInfo
    {
        public ulong ChainId { get; set; }
        public string Owner { get; set; } = "";
        public string Sequencer { get; set; } = "";
        public ulong LatestBlock { get; set; }
        public ulong LastProcessedMessageId { get; set; }
        public ulong NextMessageId { get; set; }
        public bool Registered { get; set; }
    }
}
