using Nethereum.EVM.Execution.Opcodes.Executors.Rules;
using Nethereum.EVM.Execution.CallFrame;
using Nethereum.EVM.Execution.Create.Rules;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.EVM.Execution.Storage;
using Nethereum.EVM.Execution.TransactionSetup;
using Nethereum.EVM.Execution.TransactionValidation;
using Nethereum.EVM.Execution.TxFinalisation;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Hardforks.Policies;
using Nethereum.Model.Codecs;
using Nethereum.EVM.Execution.TransferLogs.Rules;

namespace Nethereum.EVM.Hardforks
{
    public static class SpuriousDragonSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            Name = HardforkName.SpuriousDragon,

            IntrinsicGas = IntrinsicGasRuleSets.SpuriousDragon,
            Opcodes = OpcodeHandlerSets.SpuriousDragon,


            Precompiles = new[]
            {
                new PrecompileSpec { Address = 0x01, Kind = PrecompileKind.Ecrecover },
                new PrecompileSpec { Address = 0x02, Kind = PrecompileKind.Sha256 },
                new PrecompileSpec { Address = 0x03, Kind = PrecompileKind.Ripemd160 },
                new PrecompileSpec { Address = 0x04, Kind = PrecompileKind.Identity },
            },

            Validation = TransactionValidationRuleSets.Frontier,
            TouchedEmptyCleanup = Eip161TouchedEmptyCleanupRule.Instance,
            SstoreRefund = LegacySstoreRefundRule.Instance,
            GasForwarding = Eip150GasForwarding.Instance,
            CodeDeposit = HomesteadCodeDepositRule.Instance,
            ContractCreationMaterialise = MaterialiseEmptyOnSuccessRule.Instance,
            BlockHash = LegacyBlockHashRule.Instance,
            CallFrameInit = CallFrameInitRules.Empty,
            TransactionSetup = TransactionSetupRules.Empty,

            RefundQuotient = 2,
            SstoreClearsSchedule = 15000,
            SstoreSetRefund = 0,
            SstoreResetRefund = 0,
            MaxCodeSize = GasConstants.MAX_CODE_SIZE,
            MaxInitCodeSize = 0,
            MaxBlobsPerBlock = 0,
            TargetBlobsPerBlock = 0,
            ContractInitialNonce = 1,

            SystemCallGas = SystemCallGasBudget.ExecutionGasOnly,

            EnforceSstoreGasStipend = SstoreGasStipendPolicy.Disabled,
            CoinbaseAccess = CoinbaseAccessPolicy.Cold,
            BaseFee = BaseFeePolicy.MinerKeepsAll,
            EmptyAccount = EmptyAccountPolicy.Eip161Clear,
            CodePrefix = CodePrefixPolicy.Permissive,
            ReceiptCodec = LegacyReceiptCodec.Instance,
            HeaderCodec = LegacyBlockHeaderCodec.Instance,
            TransactionDecoder = LegacyOnlyTransactionDecoder.Instance,
            ReceiptConstruction = PostStateReceiptConstructionRule.Instance,

            EthTransferLog = NoEthTransferLogRule.Instance,
        };
    }
}
