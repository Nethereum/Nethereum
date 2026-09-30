namespace Nethereum.CoreChain.Services
{
    public interface IHistoricalProofCapable
    {
        bool CanServeProofAsOf(ulong blockNumber, ulong head);

        IProofService ProofServiceAsOf(ulong blockNumber);
    }
}
