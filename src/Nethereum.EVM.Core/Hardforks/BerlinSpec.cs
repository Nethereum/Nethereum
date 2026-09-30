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
    public static class BerlinSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            Name = HardforkName.Berlin,

            IntrinsicGas = IntrinsicGasRuleSets.Berlin,
            Opcodes = OpcodeHandlerSets.Berlin,

            Precompiles = new[]
            {
                new PrecompileSpec { Address = 0x01, Kind = PrecompileKind.Ecrecover },
                new PrecompileSpec { Address = 0x02, Kind = PrecompileKind.Sha256 },
                new PrecompileSpec { Address = 0x03, Kind = PrecompileKind.Ripemd160 },
                new PrecompileSpec { Address = 0x04, Kind = PrecompileKind.Identity },
                new PrecompileSpec { Address = 0x05, Kind = PrecompileKind.ModExp_Eip2565 },
                new PrecompileSpec { Address = 0x06, Kind = PrecompileKind.Bn256Add_Eip1108 },
                new PrecompileSpec { Address = 0x07, Kind = PrecompileKind.Bn256Mul_Eip1108 },
                new PrecompileSpec { Address = 0x08, Kind = PrecompileKind.Bn256Pairing_Eip1108 },
                new PrecompileSpec { Address = 0x09, Kind = PrecompileKind.Blake2 },
            },

            Validation = TransactionValidationRuleSets.Berlin,
            TouchedEmptyCleanup = Eip161TouchedEmptyCleanupRule.Instance,
            SstoreRefund = Eip1283SstoreRefundRule.Instance,
            GasForwarding = Eip150GasForwarding.Instance,
            CodeDeposit = HomesteadCodeDepositRule.Instance,
            ContractCreationMaterialise = MaterialiseEmptyOnSuccessRule.Instance,
            BlockHash = LegacyBlockHashRule.Instance,
            CallFrameInit = CallFrameInitRules.Empty,
            TransactionSetup = TransactionSetupRules.Empty,

            RefundQuotient = 2,
            SstoreClearsSchedule = 15000,
            SstoreSetRefund = 19900,
            SstoreResetRefund = 2800,
            MaxCodeSize = GasConstants.MAX_CODE_SIZE,
            MaxInitCodeSize = 0,
            MaxBlobsPerBlock = 0,
            TargetBlobsPerBlock = 0,
            ContractInitialNonce = 1,

            SystemCallGas = SystemCallGasBudget.ExecutionGasOnly,

            EnforceSstoreGasStipend = SstoreGasStipendPolicy.Eip2200Active,
            CoinbaseAccess = CoinbaseAccessPolicy.Cold,
            BaseFee = BaseFeePolicy.MinerKeepsAll,
            EmptyAccount = EmptyAccountPolicy.Eip161Clear,
            CodePrefix = CodePrefixPolicy.Permissive,
            ReceiptCodec = Eip2718ReceiptCodec.Instance,
            HeaderCodec = LegacyBlockHeaderCodec.Instance,
            TransactionDecoder = Eip2930TransactionDecoder.Instance,
            ReceiptConstruction = StatusReceiptConstructionRule.Instance,

            EthTransferLog = NoEthTransferLogRule.Instance,
        };
    }
}
