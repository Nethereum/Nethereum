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
        public string Sender { get; set; }
        public bool Success { get; set; }
        public bool Skipped { get; set; }
        public bool IsRevert { get; set; }
        public bool IsOutOfGas { get; set; }
        public BigInteger GasUsed { get; set; }
        public BigInteger CumulativeGasUsed { get; set; }
        public string ContractAddress { get; set; }
        public byte[] ReturnData { get; set; }
        public List<Log> Logs { get; set; } = new List<Log>();
        public Receipt Receipt { get; set; }
        public string RevertReason { get; set; }

        public TransactionError ErrorCode { get; set; }

        public bool RefusedByValidationRule => Skipped && ErrorCode != TransactionError.None;

        public List<ProgramTrace> Traces { get; set; }
        public BigInteger EffectiveGasPrice { get; set; }

        public long ExecutionGasUsed { get; set; }
        public long StateGasUsed { get; set; }
        public long GasRefund { get; set; }
    }
}
