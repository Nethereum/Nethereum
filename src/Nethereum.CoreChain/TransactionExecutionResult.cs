using System.Collections.Generic;
using System.Numerics;
using Nethereum.EVM;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public class TransactionExecutionResult
    {
        public ISignedTransaction Transaction { get; set; }
        public byte[] TransactionHash { get; set; }
        public int TransactionIndex { get; set; }
        public bool Success { get; set; }
        public bool Skipped { get; set; }
        public BigInteger GasUsed { get; set; }
        public BigInteger CumulativeGasUsed { get; set; }
        public string ContractAddress { get; set; }
        public byte[] ReturnData { get; set; }
        public List<Log> Logs { get; set; } = new List<Log>();
        public Receipt Receipt { get; set; }
        public string RevertReason { get; set; }

        /// <summary>
        /// Per-opcode trace of the EVM execution. Populated only when the
        /// caller passes <c>traceEnabled: true</c> to
        /// <see cref="TransactionProcessor.ExecuteTransactionAsync"/>. Used by
        /// the trace-diff debug harness to find the first opcode where
        /// Nethereum diverges from canonical execution.
        /// </summary>
        public List<ProgramTrace> Traces { get; set; }
        /// <summary>
        /// Effective gas price the sender actually paid for this tx, in wei
        /// per gas. For legacy and EIP-2930 txs, equals <c>gasPrice</c>. For
        /// EIP-1559 / EIP-4844 / EIP-7702, equals <c>baseFee + min(maxPriorityFee, maxFeePerGas - baseFee)</c>.
        /// Set by <see cref="TransactionProcessor"/> after execution from the
        /// txData's <c>GetEffectiveGasPrice(baseFee)</c>. Consumed by
        /// <see cref="BlockImporter"/>'s archive-persistence path so
        /// <c>IReceiptStore.SaveAsync</c> records the correct receipt value.
        /// </summary>
        public BigInteger EffectiveGasPrice { get; set; }
    }
}
