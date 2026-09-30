namespace Nethereum.CoreChain.DataAvailability
{
    public class CalldataCommitment
    {
        public byte[] AnchorTxHash { get; init; }
        public int Offset { get; init; }
        public int Length { get; init; }
        public byte[] ContentHash { get; init; }
    }
}
