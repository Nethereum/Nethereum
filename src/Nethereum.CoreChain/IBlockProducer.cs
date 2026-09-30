using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Documentation;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public interface IBlockProducer
    {
        [NethereumDocExample(DocSection.ChainInfrastructure, "corechain", "IBlockProducer.ProduceBlockAsync — seal a block from a transaction list")]
        Task<BlockProductionResult> ProduceBlockAsync(
            IReadOnlyList<ISignedTransaction> transactions,
            BlockProductionOptions options);
    }
}
