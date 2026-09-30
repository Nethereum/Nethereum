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
    public static class AmsterdamSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            Name = HardforkName.Amsterdam,

            IntrinsicGas = IntrinsicGasRuleSets.Amsterdam,
            Opcodes = OpcodeHandlerSets.Amsterdam,
            Validation = TransactionValidationRuleSets.Amsterdam,
            CallFrameInit = CallFrameInitRuleSets.Amsterdam,
            TransactionSetup = TransactionSetupRuleSets.Amsterdam,
            SstoreRefund = Eip8038SstoreRefundRule.Instance,
            SystemCallGas = SystemCallGasBudget.Eip8037,
            EthTransferLog = Eip7708EthTransferLogRule.Instance,
            HeaderCodec = AmsterdamBlockHeaderCodec.Instance,

            SstoreClearsSchedule = GasConstants.EIP8038_REFUND_STORAGE_CLEAR,
            SstoreSetRefund = GasConstants.EIP8038_STORAGE_WRITE,
            SstoreResetRefund = GasConstants.EIP8038_STORAGE_WRITE,
            MaxCodeSize = GasConstants.EIP7954_MAX_CODE_SIZE,
            MaxInitCodeSize = GasConstants.EIP7954_MAX_INITCODE_SIZE,
            MaxBlobsPerBlock = 21,
            TargetBlobsPerBlock = 14,

            TouchedEmptyCleanup = Eip161TouchedEmptyCleanupRule.Instance,
            GasForwarding = Eip150GasForwarding.Instance,
            CodeDeposit = HomesteadCodeDepositRule.Instance,
            ContractCreationMaterialise = MaterialiseEmptyOnSuccessRule.Instance,
            BlockHash = LegacyBlockHashRule.Instance,
            RefundQuotient = 5,
            ContractInitialNonce = 1,
            EnforceSstoreGasStipend = SstoreGasStipendPolicy.Eip2200Active,
            CoinbaseAccess = CoinbaseAccessPolicy.Eip3651Warm,
            BaseFee = BaseFeePolicy.Eip1559Burnt,
            EmptyAccount = EmptyAccountPolicy.Eip161Clear,
            CodePrefix = CodePrefixPolicy.Eip3541RejectEf,
            ReceiptCodec = Eip2718ReceiptCodec.Instance,
            TransactionDecoder = Eip7702TransactionDecoder.Instance,
            ReceiptConstruction = StatusReceiptConstructionRule.Instance,

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
        };
    }
}
