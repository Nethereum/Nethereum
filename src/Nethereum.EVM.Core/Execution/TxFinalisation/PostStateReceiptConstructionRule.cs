using System.Collections.Generic;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.TxFinalisation
{
    public sealed class PostStateReceiptConstructionRule : IReceiptConstructionRule
    {
        public static readonly PostStateReceiptConstructionRule Instance = new PostStateReceiptConstructionRule();
        private PostStateReceiptConstructionRule() { }

        public bool RequiresIntermediatePostStateRoot => true;

        public Receipt Construct(
            bool success,
            EvmUInt256 cumulativeGasUsed,
            byte[] bloom,
            List<Log> logs,
            byte[] intermediatePostStateRoot)
        {
            if (intermediatePostStateRoot == null)
                return Receipt.CreateStatusReceipt(success, cumulativeGasUsed, bloom, logs);
            return Receipt.CreatePostStateReceipt(intermediatePostStateRoot, cumulativeGasUsed, bloom, logs);
        }
    }
}
