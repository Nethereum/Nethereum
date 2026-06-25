using System.Collections.Generic;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.EVM.Execution.TxFinalisation
{
    /// <summary>
    /// EIP-658 (Byzantium onward): receipt's first field is a 1-byte
    /// status (0x01 success / 0x00 failure). No intermediate state root
    /// captured during execution. AppChain specs typically register this
    /// from genesis.
    /// </summary>
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
