using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.Model;

namespace Nethereum.AppChain.Sequencer
{
    public interface IBlockProducer
    {
        Task<BlockProductionResult> ProduceBlockAsync(IReadOnlyList<ISignedTransaction> transactions);
    }
}
