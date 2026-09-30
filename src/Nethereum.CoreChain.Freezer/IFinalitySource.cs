using Nethereum.Documentation;

namespace Nethereum.CoreChain.Freezer
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "IFinalitySource — reorg-safe finality boundary the freeze is measured against")]
    public interface IFinalitySource
    {
        long FinalizedBlockNumber { get; }
    }
}
