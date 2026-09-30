
using Nethereum.Documentation;

namespace Nethereum.Merkle.StrategyOptions.PairingConcat
{
    [NethereumDocExample(DocSection.SmartContracts, "merkle-tree", "The pluggable pair-concatenation strategy")]
    public interface IPairConcatStrategy
    {
        byte[] Concat(byte[] left, byte[] right);
    }

}
