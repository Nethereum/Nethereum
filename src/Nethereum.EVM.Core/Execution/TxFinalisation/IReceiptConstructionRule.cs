using System.Collections.Generic;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.TxFinalisation
{
    public interface IReceiptConstructionRule
    {
        bool RequiresIntermediatePostStateRoot { get; }

        Receipt Construct(
            bool success,
            EvmUInt256 cumulativeGasUsed,
            byte[] bloom,
            List<Log> logs,
            byte[] intermediatePostStateRoot);
    }
}
