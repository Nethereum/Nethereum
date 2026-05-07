namespace Nethereum.CoreChain.DataAvailability
{
    public class AnchorScope
    {
        public long ChainId { get; init; }
        public AnchorKind Kind { get; init; }
        public long StartBlock { get; init; }
        public long EndBlock { get; init; }
        public byte[] StateRoot { get; init; }
        public byte[] TransactionsRoot { get; init; }
        public byte[] ReceiptsRoot { get; init; }
        public byte[] BlockHash { get; init; }
    }
}
