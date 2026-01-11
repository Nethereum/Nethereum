using System.Collections.Generic;
using System.Numerics;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public class TransactionExecutionResult
    {
        public ISignedTransaction Transaction { get; set; }
        public byte[] TransactionHash { get; set; }
        public int TransactionIndex { get; set; }
        public bool Success { get; set; }
        public BigInteger GasUsed { get; set; }
        public BigInteger CumulativeGasUsed { get; set; }
        public string ContractAddress { get; set; }
        public byte[] ReturnData { get; set; }
        public List<Log> Logs { get; set; } = new List<Log>();
        public Receipt Receipt { get; set; }
        public string RevertReason { get; set; }
    }
}
