using Nethereum.EVM.Execution.CallFrame;
using Nethereum.EVM.Execution.Create;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.EVM.Execution.Opcodes.Executors;
using Nethereum.EVM.Execution.Storage;
using Nethereum.EVM.Execution.TransactionSetup;
using Nethereum.EVM.Execution.TransactionValidation;
using Nethereum.EVM.Execution.TransferLogs;
using Nethereum.EVM.Execution.TxFinalisation;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Hardforks.Policies;
using Nethereum.Model.Codecs;

namespace Nethereum.EVM.Hardforks
{
    public sealed record HardforkSpec
    {

        public required HardforkName Name { get; init; }


        public required IntrinsicGasRules IntrinsicGas { get; init; }

        public required PrecompileSpec[] Precompiles { get; init; }

        public required OpcodeHandlerTable Opcodes { get; init; }


        /// <summary>
        /// Transaction-type validation rules. Rejects unsupported tx
        /// types (EIP-2930 type-1, EIP-1559 type-2, EIP-4844 type-3,
        /// EIP-7702 type-4) at the active fork with the canonical
        /// error tag (e.g. <c>TR_TypeNotSupported</c>).
        ///
        /// <para><b>Common bug:</b> when this is left as
        /// <c>TransactionValidationRules.Empty</c> at a fork that
        /// SHOULD reject a tx type, the EVM silently accepts the tx and
        /// diverges from the canonical "reject + zero gas charged"
        /// state — every fork pre-London must explicitly reject EIP-1559
        /// type-2 transactions, every fork pre-Berlin must reject EIP-2930
        /// type-1.</para>
        /// </summary>
        public required TransactionValidationRules Validation { get; init; }

        public required ITouchedEmptyCleanupRule TouchedEmptyCleanup { get; init; }

        public required ISstoreRefundRule SstoreRefund { get; init; }

        public required IGasForwardingCalculator GasForwarding { get; init; }

        public required ICodeDepositRule CodeDeposit { get; init; }

        public required IContractCreationMaterialiseRule ContractCreationMaterialise { get; init; }

        public required IBlockHashRule BlockHash { get; init; }

        public required CallFrameInitRules CallFrameInit { get; init; }

        public required TransactionSetupRules TransactionSetup { get; init; }


        public required int RefundQuotient { get; init; }

        public required long SstoreClearsSchedule { get; init; }

        public required long SstoreSetRefund { get; init; }

        public required long SstoreResetRefund { get; init; }

        public required int MaxCodeSize { get; init; }

        public required int MaxInitCodeSize { get; init; }

        public required int MaxBlobsPerBlock { get; init; }

        public required int TargetBlobsPerBlock { get; init; }

        public required ulong ContractInitialNonce { get; init; }

        public required SystemCallGasBudget SystemCallGas { get; init; }


        public required SstoreGasStipendPolicy EnforceSstoreGasStipend { get; init; }

        public required CoinbaseAccessPolicy CoinbaseAccess { get; init; }

        public required BaseFeePolicy BaseFee { get; init; }

        public required EmptyAccountPolicy EmptyAccount { get; init; }

        public required CodePrefixPolicy CodePrefix { get; init; }

        public required IReceiptCodec ReceiptCodec { get; init; }

        public required IBlockHeaderCodec HeaderCodec { get; init; }

        public required ITransactionDecoder TransactionDecoder { get; init; }

        public required IReceiptConstructionRule ReceiptConstruction { get; init; }

        public required IEthTransferLogRule EthTransferLog { get; init; }
    }
}
