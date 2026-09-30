using System.Collections.Generic;
using Nethereum.Documentation;
using Nethereum.Model;

namespace Nethereum.CoreChain.Freezer
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "IChainView — pluggable read side over a live fork")]
    public interface IChainView
    {
        long HeadNumber { get; }

        byte[] BlockId(long number);

        BlockHeader Header(long number);

        IReadOnlyList<ReceiptForStorage> Receipts(long number);
    }
}
