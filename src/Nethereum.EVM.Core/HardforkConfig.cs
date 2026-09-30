using Nethereum.EVM.Execution.CallFrame;
using Nethereum.EVM.Execution.Create;
using Nethereum.EVM.Execution.Create.Rules;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.EVM.Execution.TransactionSetup;
using Nethereum.EVM.Execution.TransactionValidation;
using Nethereum.EVM.Execution.Precompiles;
using Nethereum.EVM.Gas;

namespace Nethereum.EVM
{
    public class HardforkConfig
    {
        public int MaxBlobsPerBlock { get; set; }
        public int TargetBlobsPerBlock { get; set; }
        public IntrinsicGasRules IntrinsicGasRules { get; set; }
        public PrecompileRegistry Precompiles { get; set; }
        public OpcodeHandlerTable OpcodeHandlers { get; set; }
        public CallFrameInitRules CallFrameInitRules { get; set; }
        public TransactionValidationRules TransactionValidationRules { get; set; }
        public TransactionSetupRules TransactionSetupRules { get; set; }

        public ICodeDepositRule CodeDepositRule { get; set; } = HomesteadCodeDepositRule.Instance;

        public IContractCreationMaterialiseRule ContractCreationMaterialiseRule { get; set; }
            = MaterialiseEmptyOnSuccessRule.Instance;

        public Execution.Opcodes.Executors.IBlockHashRule BlockHashRule { get; set; }
            = Execution.Opcodes.Executors.Rules.LegacyBlockHashRule.Instance;

        public IGasForwardingCalculator GasForwarding { get; set; } = Eip150GasForwarding.Instance;

        public int MaxCodeSize { get; set; } = GasConstants.MAX_CODE_SIZE;
        public int MaxInitcodeSize { get; set; } = GasConstants.MAX_INITCODE_SIZE;
        public bool RejectEfPrefix { get; set; } = true;
        public ulong ContractInitialNonce { get; set; } = 1;

        public SystemCallGasBudget SystemCallGas { get; set; } = SystemCallGasBudget.ExecutionGasOnly;

        public int RefundQuotient { get; set; } = 5;
        public long SstoreClearsSchedule { get; set; } = GasConstants.SSTORE_CLEARS_SCHEDULE;

        public long SstoreSetRefund { get; set; } = 19900;
        public long SstoreResetRefund { get; set; } = 2800;

        public bool CleanEmptyAccounts { get; set; } = true;

        public Execution.TxFinalisation.ITouchedEmptyCleanupRule TouchedEmptyCleanupRule { get; set; }
            = Execution.TxFinalisation.NoOpTouchedEmptyCleanupRule.Instance;

        public Model.Codecs.IReceiptCodec ReceiptCodec { get; set; }
            = Model.Codecs.Eip2718ReceiptCodec.Instance;

        public Model.Codecs.IBlockHeaderCodec HeaderCodec { get; set; }
            = Model.Codecs.PragueBlockHeaderCodec.Instance;

        public Model.Codecs.ITransactionDecoder TransactionDecoder { get; set; }
            = Model.Codecs.Eip7702TransactionDecoder.Instance;

        public Execution.TxFinalisation.IReceiptConstructionRule ReceiptConstruction { get; set; }
            = Execution.TxFinalisation.StatusReceiptConstructionRule.Instance;

        public Execution.TransferLogs.IEthTransferLogRule EthTransferLogRule { get; set; }
            = Execution.TransferLogs.Rules.NoEthTransferLogRule.Instance;

        /// <summary>
        /// Per-fork SSTORE refund accounting strategy. Legacy (Frontier-Byzantium
        /// and Petersburg) uses a single "non-zero to zero adds clearsSchedule"
        /// rule. EIP-1283/2200/2929 forks (Constantinople, Istanbul, Berlin+)
        /// track original-value transitions for the net-gas refund.
        /// </summary>
        public Execution.Storage.ISstoreRefundRule SstoreRefundRule { get; set; }
            = Execution.Storage.Eip1283SstoreRefundRule.Instance;

        public bool BaseFeeApplies { get; set; } = false;

        public bool EnforceSstoreGasStipend { get; set; } = false;

        public bool WarmCoinbase { get; set; } = false;

        private static HardforkConfig Build(
            IntrinsicGasRules intrinsic,
            OpcodeHandlerTable handlers,
            CallFrameInitRules callFrame = null,
            TransactionValidationRules validation = null,
            TransactionSetupRules setup = null,
            ICodeDepositRule codeDepositRule = null,
            IGasForwardingCalculator gasForwarding = null,
            int maxBlobs = 0,
            int maxCodeSize = GasConstants.MAX_CODE_SIZE,
            int maxInitcodeSize = GasConstants.MAX_INITCODE_SIZE,
            bool rejectEfPrefix = true,
            ulong contractInitialNonce = 1,
            int refundQuotient = 5,
            long sstoreClearsSchedule = GasConstants.SSTORE_CLEARS_SCHEDULE,
            long sstoreSetRefund = 19900,
            long sstoreResetRefund = 2800,
            bool cleanEmptyAccounts = true,
            bool baseFeeApplies = false,
            bool enforceSstoreGasStipend = false,
            bool warmCoinbase = false,
            Model.Codecs.IReceiptCodec receiptCodec = null,
            Model.Codecs.IBlockHeaderCodec headerCodec = null,
            Model.Codecs.ITransactionDecoder transactionDecoder = null,
            Execution.TxFinalisation.IReceiptConstructionRule receiptConstruction = null)
        {
            return new HardforkConfig
            {
                MaxBlobsPerBlock = maxBlobs,
                GasForwarding = gasForwarding ?? Eip150GasForwarding.Instance,
                MaxCodeSize = maxCodeSize,
                MaxInitcodeSize = maxInitcodeSize,
                RejectEfPrefix = rejectEfPrefix,
                ContractInitialNonce = contractInitialNonce,
                RefundQuotient = refundQuotient,
                SstoreClearsSchedule = sstoreClearsSchedule,
                SstoreSetRefund = sstoreSetRefund,
                SstoreResetRefund = sstoreResetRefund,
                CleanEmptyAccounts = cleanEmptyAccounts,
                BaseFeeApplies = baseFeeApplies,
                EnforceSstoreGasStipend = enforceSstoreGasStipend,
                WarmCoinbase = warmCoinbase,
                TouchedEmptyCleanupRule = cleanEmptyAccounts
                    ? (Execution.TxFinalisation.ITouchedEmptyCleanupRule)Execution.TxFinalisation.Eip161TouchedEmptyCleanupRule.Instance
                    : Execution.TxFinalisation.NoOpTouchedEmptyCleanupRule.Instance,
                SstoreRefundRule = (sstoreSetRefund == 0 && sstoreResetRefund == 0)
                    ? (Execution.Storage.ISstoreRefundRule)Execution.Storage.LegacySstoreRefundRule.Instance
                    : Execution.Storage.Eip1283SstoreRefundRule.Instance,
                CodeDepositRule = codeDepositRule ?? HomesteadCodeDepositRule.Instance,
                IntrinsicGasRules = intrinsic,
                OpcodeHandlers = handlers.Freeze(),
                CallFrameInitRules = callFrame ?? CallFrameInitRules.Empty,
                TransactionValidationRules = validation ?? TransactionValidationRules.Empty,
                TransactionSetupRules = setup ?? TransactionSetupRules.Empty,
                ReceiptCodec = receiptCodec ?? (cleanEmptyAccounts
                    ? (Model.Codecs.IReceiptCodec)Model.Codecs.Eip2718ReceiptCodec.Instance
                    : Model.Codecs.LegacyReceiptCodec.Instance),
                HeaderCodec = headerCodec ?? (cleanEmptyAccounts
                    ? (Model.Codecs.IBlockHeaderCodec)Model.Codecs.PragueBlockHeaderCodec.Instance
                    : Model.Codecs.LegacyBlockHeaderCodec.Instance),
                TransactionDecoder = transactionDecoder ?? (cleanEmptyAccounts
                    ? (Model.Codecs.ITransactionDecoder)Model.Codecs.Eip7702TransactionDecoder.Instance
                    : Model.Codecs.LegacyOnlyTransactionDecoder.Instance),
                ReceiptConstruction = receiptConstruction ?? (cleanEmptyAccounts
                    ? (Execution.TxFinalisation.IReceiptConstructionRule)Execution.TxFinalisation.StatusReceiptConstructionRule.Instance
                    : Execution.TxFinalisation.PostStateReceiptConstructionRule.Instance),
            };
        }


        private static readonly System.Lazy<HardforkConfig> _frontier = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.FrontierSpec.Instance));
        private static readonly System.Lazy<HardforkConfig> _homestead = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.HomesteadSpec.Instance));
        private static readonly System.Lazy<HardforkConfig> _tangerineWhistle = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.TangerineWhistleSpec.Instance));
        private static readonly System.Lazy<HardforkConfig> _spuriousDragon = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.SpuriousDragonSpec.Instance));
        private static readonly System.Lazy<HardforkConfig> _byzantium = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.ByzantiumSpec.Instance));
        private static readonly System.Lazy<HardforkConfig> _constantinople = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.ConstantinopleSpec.Instance));
        private static readonly System.Lazy<HardforkConfig> _petersburg = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.PetersburgSpec.Instance));
        private static readonly System.Lazy<HardforkConfig> _istanbul = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.IstanbulSpec.Instance));
        private static readonly System.Lazy<HardforkConfig> _berlin = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.BerlinSpec.Instance));
        private static readonly System.Lazy<HardforkConfig> _london = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.LondonSpec.Instance));
        private static readonly System.Lazy<HardforkConfig> _paris = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.ParisSpec.Instance));
        private static readonly System.Lazy<HardforkConfig> _shanghai = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.ShanghaiSpec.Instance));
        private static readonly System.Lazy<HardforkConfig> _cancun = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.CancunSpec.Instance));
        private static readonly System.Lazy<HardforkConfig> _prague = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.PragueSpec.Instance));

        private static readonly System.Lazy<HardforkConfig> _osaka = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.OsakaSpec.Instance));
        private static readonly System.Lazy<HardforkConfig> _osakaBpo1 = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.OsakaBpo1Spec.Instance));
        private static readonly System.Lazy<HardforkConfig> _osakaBpo2 = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.OsakaBpo2Spec.Instance));
        private static readonly System.Lazy<HardforkConfig> _amsterdam = new(() => Hardforks.HardforkConfigFromSpec.Build(Hardforks.AmsterdamSpec.Instance));

        public static HardforkConfig Frontier => _frontier.Value;
        public static HardforkConfig Homestead => _homestead.Value;
        public static HardforkConfig TangerineWhistle => _tangerineWhistle.Value;
        public static HardforkConfig SpuriousDragon => _spuriousDragon.Value;
        public static HardforkConfig Byzantium => _byzantium.Value;
        public static HardforkConfig Constantinople => _constantinople.Value;
        public static HardforkConfig Petersburg => _petersburg.Value;
        public static HardforkConfig Istanbul => _istanbul.Value;
        public static HardforkConfig Berlin => _berlin.Value;
        public static HardforkConfig London => _london.Value;
        public static HardforkConfig Paris => _paris.Value;
        public static HardforkConfig Shanghai => _shanghai.Value;
        public static HardforkConfig Cancun => _cancun.Value;
        public static HardforkConfig Prague => _prague.Value;
        public static HardforkConfig Osaka => _osaka.Value;
        public static HardforkConfig OsakaBpo1 => _osakaBpo1.Value;
        public static HardforkConfig OsakaBpo2 => _osakaBpo2.Value;
        public static HardforkConfig Amsterdam => _amsterdam.Value;

        public HardforkConfig Clone() => (HardforkConfig)MemberwiseClone();
    }
}
