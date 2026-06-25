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
        public IntrinsicGasRules IntrinsicGasRules { get; set; }
        public PrecompileRegistry Precompiles { get; set; }
        public OpcodeHandlerTable OpcodeHandlers { get; set; }
        public CallFrameInitRules CallFrameInitRules { get; set; }
        public TransactionValidationRules TransactionValidationRules { get; set; }
        public TransactionSetupRules TransactionSetupRules { get; set; }

        // CREATE code deposit: Frontier allows empty-code deploy on OOG, Homestead+ reverts
        public ICodeDepositRule CodeDepositRule { get; set; } = HomesteadCodeDepositRule.Instance;

        /// <summary>
        /// CREATE-transaction materialisation rule — invoked when a CREATE
        /// transaction has empty init data + zero value (no init code, no
        /// transfer), commits the snapshot so the empty contract account
        /// persists in state. Uniform across forks; the create path always
        /// creates the account.
        /// </summary>
        public IContractCreationMaterialiseRule ContractCreationMaterialiseRule { get; set; }
            = MaterialiseEmptyOnSuccessRule.Instance;

        /// <summary>
        /// BLOCKHASH opcode resolution rule. Legacy (Frontier→Cancun)
        /// walks the block store; EIP-2935 (Prague+) reads history
        /// contract storage. Default: legacy, since Prague isn't shipped
        /// to mainnet yet.
        /// </summary>
        public Execution.Opcodes.Executors.IBlockHashRule BlockHashRule { get; set; }
            = Execution.Opcodes.Executors.Rules.LegacyBlockHashRule.Instance;

        public IGasForwardingCalculator GasForwarding { get; set; } = Eip150GasForwarding.Instance;

        // CREATE validation: per-fork limits
        public int MaxCodeSize { get; set; } = 24576;     // EIP-170 (Spurious Dragon+). 0 = no limit (Frontier-Tangerine)
        public int MaxInitcodeSize { get; set; } = 49152;  // EIP-3860 (Shanghai+). 0 = no limit (pre-Shanghai)
        public bool RejectEfPrefix { get; set; } = true;   // EIP-3541 (London+). false = allow 0xEF deployed code
        public ulong ContractInitialNonce { get; set; } = 1;  // EIP-161 (Spurious Dragon+). 0 = Frontier-Tangerine

        // Per-fork refund constants
        public int RefundQuotient { get; set; } = 5;
        public long SstoreClearsSchedule { get; set; } = 4800;

        // EIP-1283 / EIP-2200 / EIP-2929 net-gas refund values.
        // Constantinople (EIP-1283, SLOAD=200): 20000-200=19800, 5000-200=4800
        // Petersburg (EIP-1283 reverted): 0, 0 (dirty branch is a no-op)
        // Istanbul (EIP-2200, SLOAD=800): 20000-800=19200, 5000-800=4200
        // Berlin+ (EIP-2929, SLOAD=100, RESET=2900): 20000-100=19900, 2900-100=2800
        public long SstoreSetRefund { get; set; } = 19900;
        public long SstoreResetRefund { get; set; } = 2800;

        // EIP-161 (Spurious Dragon+): empty accounts removed from state root
        // Pre-Spurious Dragon: empty accounts remain in state root.
        // Kept for back-compat of consumers reading the flag. The executor
        // uses TouchedEmptyCleanupRule below as the strategy.
        public bool CleanEmptyAccounts { get; set; } = true;

        /// <summary>
        /// EIP-161 STATE_CLEARING strategy. NoOp at pre-EIP-161 forks,
        /// Eip161 at Spurious Dragon onwards. The executor invokes
        /// <c>TouchedEmptyCleanupRule.Apply(executionState)</c>
        /// unconditionally at end-of-tx; the per-fork choice is the
        /// single point of configuration.
        /// </summary>
        public Execution.TxFinalisation.ITouchedEmptyCleanupRule TouchedEmptyCleanupRule { get; set; }
            = Execution.TxFinalisation.NoOpTouchedEmptyCleanupRule.Instance;

        /// <summary>
        /// Per-fork receipt codec — canonical RLP form used by the receipts-
        /// trie, storage layer, and peer-serving paths. Defaults to
        /// <see cref="Model.Codecs.Eip2718ReceiptCodec.Instance"/> which
        /// encodes typed-envelope receipts when <see cref="Receipt.TransactionType"/>
        /// is non-zero (Berlin onward) and bare RLP lists otherwise. This
        /// is the strictly more permissive choice — accepts both legacy
        /// and typed wire shapes on decode. Pre-EIP-2718 fork consumers
        /// should set <see cref="Model.Codecs.LegacyReceiptCodec.Instance"/>
        /// explicitly to reject typed envelopes. Production paths build via
        /// <see cref="HardforkConfigFromSpec.Build"/>, which sets the
        /// fork-correct codec from the spec file.
        /// </summary>
        public Model.Codecs.IReceiptCodec ReceiptCodec { get; set; }
            = Model.Codecs.Eip2718ReceiptCodec.Instance;

        /// <summary>
        /// Per-fork block-header codec. Default <see cref="Model.Codecs.PragueBlockHeaderCodec.Instance"/>
        /// matches the current mainnet head. Production paths set the
        /// fork-correct codec via <see cref="HardforkConfigFromSpec.Build"/>.
        /// Imperative callers building for older forks must pass an
        /// explicit header codec or override after construction.
        /// </summary>
        public Model.Codecs.IBlockHeaderCodec HeaderCodec { get; set; }
            = Model.Codecs.PragueBlockHeaderCodec.Instance;

        /// <summary>
        /// Per-fork transaction decoder. Default
        /// <see cref="Model.Codecs.Eip7702TransactionDecoder.Instance"/>
        /// matches current mainnet head (Osaka inherits Prague's tx-type
        /// set including 0x04 EIP-7702). Pre-Prague consumers should
        /// override with the fork-correct decoder via
        /// <see cref="HardforkConfigFromSpec.Build"/>.
        /// </summary>
        public Model.Codecs.ITransactionDecoder TransactionDecoder { get; set; }
            = Model.Codecs.Eip7702TransactionDecoder.Instance;

        /// <summary>
        /// Per-fork receipt construction rule (EIP-658 gate). Default
        /// <see cref="Execution.TxFinalisation.StatusReceiptConstructionRule.Instance"/>
        /// matches current mainnet head (status-form, Byzantium+). Pre-Byzantium
        /// consumers must override via <see cref="HardforkConfigFromSpec.Build"/>
        /// or by passing the rule explicitly.
        /// </summary>
        public Execution.TxFinalisation.IReceiptConstructionRule ReceiptConstruction { get; set; }
            = Execution.TxFinalisation.StatusReceiptConstructionRule.Instance;

        /// <summary>
        /// Per-fork SSTORE refund accounting strategy. Legacy (Frontier-Byzantium
        /// and Petersburg) uses a single "non-zero to zero adds clearsSchedule"
        /// rule. EIP-1283/2200/2929 forks (Constantinople, Istanbul, Berlin+)
        /// track original-value transitions for the net-gas refund.
        /// </summary>
        public Execution.Storage.ISstoreRefundRule SstoreRefundRule { get; set; }
            = Execution.Storage.Eip1283SstoreRefundRule.Instance;

        // EIP-1559 (London+): coinbase receives only the priority tip
        // (effectiveGasPrice - baseFee), not the full effectiveGasPrice. The
        // baseFee portion is burnt. Pre-London the full gas fee goes to the
        // miner — modelled by leaving this flag false so TransactionExecutor
        // pays `gasUsed * effectiveGasPrice` directly.
        public bool BaseFeeApplies { get; set; } = false;

        // EIP-2200 (Istanbul+): SSTORE reentrancy sentry — opcode OOGs when
        // gas remaining <= 2300.
        // EIP-1283 (Constantinople, Petersburg) and earlier do NOT have the
        // sentry; pre-EIP-2200 SSTORE can run on as little as 200 gas (the
        // EIP-1283 NOOP path). Setting this true at Constantinople would
        // OOG legitimate deep-recursion SSTOREs.
        public bool EnforceSstoreSentry { get; set; } = false;

        // EIP-3651 (Shanghai+): coinbase pre-warmed before tx execution so
        // first BALANCE/EXTCODE*/CALL to the coinbase costs 100 (warm)
        // instead of 2600 (cold). Pre-Shanghai (Berlin/London/Paris) leave
        // coinbase cold.
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
            int maxCodeSize = 24576,
            int maxInitcodeSize = 49152,
            bool rejectEfPrefix = true,
            ulong contractInitialNonce = 1,
            int refundQuotient = 5,
            long sstoreClearsSchedule = 4800,
            long sstoreSetRefund = 19900,
            long sstoreResetRefund = 2800,
            bool cleanEmptyAccounts = true,
            bool baseFeeApplies = false,
            bool enforceSstoreSentry = false,
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
                EnforceSstoreSentry = enforceSstoreSentry,
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
                // Default to legacy codec when no codec is passed AND the
                // sentinel choice "cleanEmptyAccounts == false" indicates a
                // pre-EIP-158 fork shape — those will also be pre-EIP-2718.
                // Imperative callers building post-Berlin configs must pass
                // an explicit codec. Production code uses HardforkConfigFromSpec.
                ReceiptCodec = receiptCodec ?? (cleanEmptyAccounts
                    ? (Model.Codecs.IReceiptCodec)Model.Codecs.Eip2718ReceiptCodec.Instance
                    : Model.Codecs.LegacyReceiptCodec.Instance),
                // Header codec: default to Legacy (15 fields, no baseFee) when
                // caller is building a pre-EIP-158 config sentinel; otherwise
                // Prague (current head). Production paths use
                // HardforkConfigFromSpec.Build which sets fork-correct codec.
                HeaderCodec = headerCodec ?? (cleanEmptyAccounts
                    ? (Model.Codecs.IBlockHeaderCodec)Model.Codecs.PragueBlockHeaderCodec.Instance
                    : Model.Codecs.LegacyBlockHeaderCodec.Instance),
                // Transaction decoder: default Prague (current head) if
                // post-EIP-158 sentinel, else legacy-only. Production paths
                // use HardforkConfigFromSpec.Build for fork-correct gating.
                TransactionDecoder = transactionDecoder ?? (cleanEmptyAccounts
                    ? (Model.Codecs.ITransactionDecoder)Model.Codecs.Eip7702TransactionDecoder.Instance
                    : Model.Codecs.LegacyOnlyTransactionDecoder.Instance),
                // Receipt construction: default status-form (Byzantium+) when
                // cleanEmptyAccounts (EIP-158 sentinel) is true; PostState
                // when caller signals a pre-EIP-158 config (which also
                // happens to be pre-EIP-658). Production paths use
                // HardforkConfigFromSpec.Build for fork-correct gating.
                ReceiptConstruction = receiptConstruction ?? (cleanEmptyAccounts
                    ? (Execution.TxFinalisation.IReceiptConstructionRule)Execution.TxFinalisation.StatusReceiptConstructionRule.Instance
                    : Execution.TxFinalisation.PostStateReceiptConstructionRule.Instance),
            };
        }

        // Every Lazy initializer below sources from the corresponding
        // Hardforks/<Fork>Spec.cs declarative spec via HardforkConfigFromSpec.
        // The spec is the source of truth; this runtime view is derived from it.
        // The Build(...) factory above is kept for callers that still build a
        // HardforkConfig imperatively.

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

        /// <summary>
        /// Shallow copy. Used by registry-layering helpers (e.g.
        /// <c>KzgAwareMainnetHardforkRegistry</c>) that need to swap one
        /// field — <see cref="Precompiles"/> — without mutating the
        /// shared <see cref="Nethereum.EVM.Precompiles.DefaultMainnetHardforkRegistry"/>
        /// singleton. Reference-type fields (registries, rule strategies,
        /// opcode tables) are shared by design — every fork's rule
        /// strategies are themselves singletons, so a shallow copy is
        /// safe and the caller can replace any single slot in isolation.
        /// </summary>
        public HardforkConfig Clone() => (HardforkConfig)MemberwiseClone();
    }
}
