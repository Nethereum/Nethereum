namespace Nethereum.EVM
{
    /// <summary>
    /// Resolves the active hardfork for a given block/timestamp on a specific chain.
    /// Pre-Shanghai forks activate at a block number; Shanghai onward activate at a
    /// timestamp. Implementations are chain-specific (mainnet, L2s, AppChains) and
    /// registered with <c>ChainActivationsRegistry</c> keyed by chain id.
    /// </summary>
    public interface IChainActivations
    {
        HardforkName ResolveAt(long blockNumber, ulong timestamp);
    }
}
