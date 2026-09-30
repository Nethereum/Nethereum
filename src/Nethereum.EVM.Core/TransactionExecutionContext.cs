using Nethereum.Documentation;
using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
using Nethereum.Util;
using Nethereum.EVM.Gas;
using Nethereum.Model;
using Nethereum.EVM.Execution.TransferLogs;

namespace Nethereum.EVM
{
    [NethereumDocExample(DocSection.EvmSimulator, "simulate-transaction", "Whether the executor is running a transaction, a call, or a system call")]
    public enum ExecutionMode
    {
        Transaction,
        Call,
        SystemCall
    }

    public class TransactionExecutionContext
    {
        public ExecutionMode Mode { get; set; } = ExecutionMode.Transaction;
        public bool IsCallMode => Mode == ExecutionMode.Call || Mode == ExecutionMode.SystemCall;
        public string Sender { get; set; }
        public string To { get; set; }
        public byte[] Data { get; set; }

        public EvmUInt256 GasLimit { get; set; }
        public EvmUInt256 Value { get; set; }
        public EvmUInt256 GasPrice { get; set; }
        public EvmUInt256 MaxFeePerGas { get; set; }
        public EvmUInt256 MaxPriorityFeePerGas { get; set; }
        public EvmUInt256 EffectiveGasPrice { get; set; }
        public EvmUInt256 Nonce { get; set; }
        public bool IsEip1559 { get; set; }
        public bool IsContractCreation { get; set; }
        public bool IsType3Transaction { get; set; }
        public TransactionType TransactionType { get; set; } = TransactionType.LegacyTransaction;
        public List<string> BlobVersionedHashes { get; set; }
        public EvmUInt256 MaxFeePerBlobGas { get; set; }
        public List<AccessListEntry> AccessList { get; set; }
        public List<Authorisation7702Signed> AuthorisationList { get; set; }

        public List<string> AuthorisationAuthorities { get; set; }

        public EvmUInt256 BlockNumber { get; set; }
        public EvmUInt256 Timestamp { get; set; }
        public string Coinbase { get; set; }
        public EvmUInt256 BaseFee { get; set; }
        public EvmUInt256 Difficulty { get; set; }
        public EvmUInt256 SlotNumber { get; set; } = EvmUInt256.Zero;
        public EvmUInt256 BlockGasLimit { get; set; }

        public Gas.BlockGasCapacity BlockGasCapacity
        {
            get => _blockGasCapacity ??= new Gas.BlockGasCapacity();
            set => _blockGasCapacity = value;
        }
        private Gas.BlockGasCapacity _blockGasCapacity;

        public EvmUInt256 ExcessBlobGas { get; set; }
        public EvmUInt256 BlobBaseFee { get; set; } = EvmUInt256.Zero;
        public EvmUInt256 ChainId { get; set; }

        public EvmUInt256? DeclaredChainId { get; set; }

        public EvmUInt256 Fee { get; set; }

        public long IntrinsicExecutionGas { get; set; }
        public long FloorGas { get; set; }
        public long MinGasRequired { get; set; }
        public long AuthRefund { get; set; }
        public EvmUInt256 BlobGasCost { get; set; }

        public long ExecutionGasGrant { get; set; }
        public long StateGasReservoir { get; set; }

        public Gas.StateGasAccount StateGas { get; set; } = new Gas.StateGasAccount();

        public long StateGasUsed => StateGas.FromReservoir + StateGas.SpilledIntoExecution;

        public string ContractAddress { get; set; }
        public EvmUInt256 SenderNonceBeforeIncrement { get; set; }

        public const int NotTaken = -1;

        public int TransactionSnapshotId { get; set; } = NotTaken;

        public int PrepPhaseSnapshotId { get; set; } = NotTaken;

        public bool HasCollision { get; set; }

        public bool AuthPrepFailed { get; set; }

        public bool CodeResolutionFailed { get; set; }

        public long TotalForfeitGasUsed() => IntrinsicExecutionGas + ExecutionGasGrant;

        public bool TryChargeStateGas(long amount, out long spilledIntoExecution)
        {
            spilledIntoExecution = 0;
            if (amount <= 0) return true;

            if (StateGas.ReservoirRemaining >= amount)
            {
                StateGas.ReservoirRemaining -= amount;
                StateGas.FromReservoir += amount;
                return true;
            }

            var remainder = amount - StateGas.ReservoirRemaining;
            if (PreDispatchExecutionGasAvailable < remainder)
                return false;

            StateGas.FromReservoir += StateGas.ReservoirRemaining;
            StateGas.ReservoirRemaining = 0;
            StateGas.SpilledIntoExecution += remainder;
            spilledIntoExecution = remainder;
            return true;
        }

        public long PreDispatchExecutionGasCharged { get; set; }

        public long PreDispatchExecutionGasAvailable
            => ExecutionGasGrant - PreDispatchExecutionGasConsumed;

        public long PreDispatchExecutionGasConsumed
            => StateGas.SpilledIntoExecution + PreDispatchExecutionGasCharged;

        public bool NewAccountStateGasCharged { get; set; }

        public ExecutionStateService ExecutionState { get; set; }
        public AccountExecutionState SenderAccount { get; set; }
        public byte[] Code { get; set; }
        public string DelegateAddress { get; set; }

        public bool TraceEnabled { get; set; }

        public bool TrackAccessList { get; set; }

        public IEthTransferLogRule EthTransferLogRuleOverride { get; set; }

        public bool EnforceSenderBalance { get; set; }

        public bool SettleTransactionFees { get; set; }

        public bool AllowFeeCapBelowBaseFee { get; set; }

        public bool AdvanceSenderNonce { get; set; }

        /// <summary>
        /// eth_simulateV1 with validation off (geth SkipNonceChecks): the sender nonce is not validated and a
        /// sender already at 2^64-1 is not rejected with NONCE_IS_MAX - the nonce simply wraps modulo 2^64 as
        /// geth's uint64 increment does. Default false keeps EIP-2681 strict on every committing/validating path.
        /// </summary>
        public bool SkipNonceMaxCheck { get; set; }

        public bool PreserveZeroBaseFee { get; set; }

        public System.Collections.Generic.IReadOnlyDictionary<string, int> PrecompileRelocations { get; set; }

        public Execution.Opcodes.Executors.IBlockHashRule BlockHashRuleOverride { get; set; }
    }
}
