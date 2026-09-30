namespace Nethereum.CoreChain.Services
{
    public interface IHistoricalProofServingBundle
    {
        IHistoricalProofCapable NodeServing { get; }
    }
}
