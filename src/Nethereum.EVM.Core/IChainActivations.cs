namespace Nethereum.EVM
{
    public interface IChainActivations
    {
        HardforkName ResolveAt(long blockNumber, ulong timestamp);
    }
}
