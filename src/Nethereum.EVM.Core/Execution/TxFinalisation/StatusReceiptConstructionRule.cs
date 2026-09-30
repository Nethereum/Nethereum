using System.Collections.Generic;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.TxFinalisation
{
    public sealed class StatusReceiptConstructionRule : IReceiptConstructionRule
    {
        public static readonly StatusReceiptConstructionRule Instance = new StatusReceiptConstructionRule();
        private StatusReceiptConstructionRule() { }

        public bool RequiresIntermediatePostStateRoot => false;

        public Receipt Construct(
            bool success,
            EvmUInt256 cumulativeGasUsed,
            byte[] bloom,
            List<Log> logs,
            byte[] intermediatePostStateRoot)
            => Receipt.CreateStatusReceipt(success, cumulativeGasUsed, bloom, logs);
    }
}
