using System.Collections.Generic;
using System.Numerics;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.Model;

namespace Nethereum.EVM
{
    public class TransactionExecutionContext
    {
        public string Sender { get; set; }
        public string To { get; set; }
        public byte[] Data { get; set; }
        public BigInteger GasLimit { get; set; }
        public BigInteger Value { get; set; }
        public BigInteger GasPrice { get; set; }
        public BigInteger MaxFeePerGas { get; set; }
        public BigInteger MaxPriorityFeePerGas { get; set; }
        public BigInteger EffectiveGasPrice { get; set; }
        public BigInteger Nonce { get; set; }
        public bool IsEip1559 { get; set; }
        public bool IsContractCreation { get; set; }
        public bool IsType3Transaction { get; set; }
        public bool IsType4Transaction { get; set; }
        public List<string> BlobVersionedHashes { get; set; }
        public BigInteger MaxFeePerBlobGas { get; set; }
        public List<AccessListEntry> AccessList { get; set; }
        public List<Authorisation7702Signed> AuthorisationList { get; set; }

        public long BlockNumber { get; set; }
        public long Timestamp { get; set; }
        public string Coinbase { get; set; }
        public BigInteger BaseFee { get; set; }
        public BigInteger Difficulty { get; set; }
        public BigInteger BlockGasLimit { get; set; }
        public BigInteger ExcessBlobGas { get; set; }
        public BigInteger BlobBaseFee { get; set; }
        public BigInteger ChainId { get; set; }

        public BigInteger IntrinsicGas { get; set; }
        public BigInteger FloorGas { get; set; }
        public BigInteger MinGasRequired { get; set; }
        public BigInteger BlobGasCost { get; set; }

        public string ContractAddress { get; set; }
        public BigInteger SenderNonceBeforeIncrement { get; set; }

        public int TransactionSnapshotId { get; set; }
        public bool HasCollision { get; set; }

        public ExecutionStateService ExecutionState { get; set; }
        public AccountExecutionState SenderAccount { get; set; }
        public byte[] Code { get; set; }
        public string DelegateAddress { get; set; }

        public bool TraceEnabled { get; set; }
    }
}
