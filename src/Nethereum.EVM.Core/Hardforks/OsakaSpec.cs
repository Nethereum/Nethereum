using Nethereum.EVM.Execution.Opcodes.Executors.Rules;
using Nethereum.EVM.Execution.CallFrame;
using Nethereum.EVM.Execution.Create;
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
    public static class OsakaSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            Name = HardforkName.Osaka,

            IntrinsicGas = IntrinsicGasRuleSets.Osaka,
            Opcodes = OpcodeHandlerSets.Osaka,

            Precompiles = new[]
            {
                new PrecompileSpec { Address = 0x01, Kind = PrecompileKind.Ecrecover },
                new PrecompileSpec { Address = 0x02, Kind = PrecompileKind.Sha256 },
                new PrecompileSpec { Address = 0x03, Kind = PrecompileKind.Ripemd160 },
                new PrecompileSpec { Address = 0x04, Kind = PrecompileKind.Identity },
                new PrecompileSpec { Address = 0x05, Kind = PrecompileKind.ModExp_Eip7883 },
                new PrecompileSpec { Address = 0x06, Kind = PrecompileKind.Bn256Add_Eip1108 },
                new PrecompileSpec { Address = 0x07, Kind = PrecompileKind.Bn256Mul_Eip1108 },
                new PrecompileSpec { Address = 0x08, Kind = PrecompileKind.Bn256Pairing_Eip1108 },
                new PrecompileSpec { Address = 0x09, Kind = PrecompileKind.Blake2 },
                new PrecompileSpec { Address = 0x0A, Kind = PrecompileKind.PointEvaluation },
                new PrecompileSpec { Address = 0x0B, Kind = PrecompileKind.Bls12381_G1Add },
                new PrecompileSpec { Address = 0x0C, Kind = PrecompileKind.Bls12381_G1MultiExp },
                new PrecompileSpec { Address = 0x0D, Kind = PrecompileKind.Bls12381_G2Add },
                new PrecompileSpec { Address = 0x0E, Kind = PrecompileKind.Bls12381_G2MultiExp },
                new PrecompileSpec { Address = 0x0F, Kind = PrecompileKind.Bls12381_Pairing },
                new PrecompileSpec { Address = 0x10, Kind = PrecompileKind.Bls12381_MapFpToG1 },
                new PrecompileSpec { Address = 0x11, Kind = PrecompileKind.Bls12381_MapFp2ToG2 },
                new PrecompileSpec { Address = 0x100, Kind = PrecompileKind.P256Verify },
            },

            Validation = TransactionValidationRuleSets.Osaka,

            TouchedEmptyCleanup = Eip161TouchedEmptyCleanupRule.Instance,

            SstoreRefund = Eip1283SstoreRefundRule.Instance,

            GasForwarding = Eip150GasForwarding.Instance,

            CodeDeposit = HomesteadCodeDepositRule.Instance,
            ContractCreationMaterialise = MaterialiseEmptyOnSuccessRule.Instance,
            BlockHash = LegacyBlockHashRule.Instance,

            CallFrameInit = CallFrameInitRuleSets.Osaka,
            TransactionSetup = TransactionSetupRuleSets.Osaka,

            RefundQuotient = 5,
            SstoreClearsSchedule = GasConstants.SSTORE_CLEARS_SCHEDULE,

            SstoreSetRefund = 19900,
            SstoreResetRefund = 2800,

            MaxCodeSize = GasConstants.MAX_CODE_SIZE,

            MaxInitCodeSize = GasConstants.MAX_INITCODE_SIZE,

            MaxBlobsPerBlock = 9,
            TargetBlobsPerBlock = 6,

            ContractInitialNonce = 1,

            SystemCallGas = SystemCallGasBudget.ExecutionGasOnly,

            EnforceSstoreGasStipend = SstoreGasStipendPolicy.Eip2200Active,

            CoinbaseAccess = CoinbaseAccessPolicy.Eip3651Warm,

            BaseFee = BaseFeePolicy.Eip1559Burnt,

            EmptyAccount = EmptyAccountPolicy.Eip161Clear,

            CodePrefix = CodePrefixPolicy.Eip3541RejectEf,
            ReceiptCodec = Eip2718ReceiptCodec.Instance,
            HeaderCodec = PragueBlockHeaderCodec.Instance,
            TransactionDecoder = Eip7702TransactionDecoder.Instance,
            ReceiptConstruction = StatusReceiptConstructionRule.Instance,

            EthTransferLog = NoEthTransferLogRule.Instance,
        };
    }
}
