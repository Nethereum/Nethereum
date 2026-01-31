using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public interface IBlockProducer
    {
        Task<BlockProductionResult> ProduceBlockAsync(
            IReadOnlyList<ISignedTransaction> transactions,
            BlockProductionOptions options);
    }
}
