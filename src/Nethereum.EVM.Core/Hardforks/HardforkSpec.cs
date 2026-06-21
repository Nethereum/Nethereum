using Nethereum.EVM.Execution.CallFrame;
using Nethereum.EVM.Execution.Create;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.EVM.Execution.Opcodes.Executors;
using Nethereum.EVM.Execution.SelfDestruct;
using Nethereum.EVM.Execution.Storage;
using Nethereum.EVM.Execution.TransactionSetup;
using Nethereum.EVM.Execution.TransactionValidation;
using Nethereum.EVM.Execution.TxFinalisation;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Hardforks.Policies;
using Nethereum.Model.Codecs;

namespace Nethereum.EVM.Hardforks
{
    /// <summary>
    /// Self-contained, required-field specification of a single Ethereum
    /// hardfork. Every property MUST be set explicitly at construction
    /// (enforced by C# 11 <c>required</c> keyword) — there are no silent
    /// defaults, no inheritance from a previous fork's spec, and no
    /// shared-by-reference rule lookups.
    ///
    /// <para>Each fork's spec file under <see cref="N:Nethereum.EVM.Hardforks"/>
    /// (e.g. <c>ConstantinopleSpec.cs</c>) constructs an instance of this
    /// record with every field set to a deliberately-chosen, EIP-cited
    /// value. Reading the spec file tells you exactly which EIPs are
    /// active at that fork and which are explicitly NOT active.</para>
    ///
    /// <para>The runtime <see cref="HardforkConfig"/> is produced from a
    /// <see cref="HardforkSpec"/> via <c>HardforkConfig.FromSpec(spec)</c>.
    /// HardforkConfig remains the consumer-facing API; HardforkSpec is
    /// the declarative source-of-truth that produces it.</para>
    ///
    /// <para><b>Zisk constraint:</b> this type uses only <c>init</c>
    /// properties + concrete reference fields, with no LINQ, reflection,
    /// or dynamic dispatch beyond what already runs in the existing
    /// strategy interfaces. It lowers to plain fields under bflat-riscv64.</para>
    /// </summary>
    public sealed record HardforkSpec
    {
        // ===================================================================
        // IDENTITY
        // ===================================================================

        /// <summary>
        /// Canonical name of this hardfork (e.g. <c>HardforkName.Constantinople</c>).
        /// Used by the test runner to look up post-state fixtures and by
        /// RPC responses to label the active rules to clients.
        /// </summary>
        public required HardforkName Name { get; init; }

        // ===================================================================
        // OPCODE + INTRINSIC GAS TABLES
        // ===================================================================

        /// <summary>
        /// Per-fork intrinsic gas rules — the engine that consumes a
        /// transaction (data, access list, init code, blob hashes) and
        /// returns the intrinsic gas charge. Constants
        /// (<c>TxBase</c>, <c>TxDataZero</c>, <c>TxDataNonZero</c>,
        /// <c>TxCreate</c>) and strategy slots (<c>InitCode</c>,
        /// <c>AccessList</c>, <c>Blob</c>, <c>Floor</c>) live together
        /// on <see cref="IntrinsicGasRules"/>. Reading the
        /// corresponding <c>IntrinsicGasRuleSets.X</c> constructor call
        /// shows every value in one place.
        ///
        /// <para><b>Geth ref:</b>
        /// <c>core/types/transaction_signing.go IntrinsicGas</c>.</para>
        /// </summary>
        public required IntrinsicGasRules IntrinsicGas { get; init; }

        /// <summary>
        /// Declarative list of precompiles active at this fork. Each
        /// entry specifies the address slot + the EIP-specific gas
        /// formula variant. The actual executor (cryptographic
        /// implementation) is provided lazily by the host's
        /// <see cref="IPrecompileExecutorRegistry"/> — keeps the spec
        /// portable across production Nethereum / Zisk guest / in-memory
        /// simulator.
        ///
        /// <para>Reading a fork-spec file now shows the exact set of
        /// active precompiles instead of hiding them behind external
        /// <c>WithPrecompiles(...)</c> wiring in
        /// <c>MainnetHardforkRegistry</c>.</para>
        /// </summary>
        public required PrecompileSpec[] Precompiles { get; init; }

        /// <summary>
        /// EVM opcode dispatch table — maps each <see cref="Instruction"/>
        /// to its gas-cost and execution-handler bindings for THIS fork.
        ///
        /// <para>Built by composition of per-EIP registration methods
        /// (e.g. <c>RegisterEip7DelegateCall</c>,
        /// <c>RegisterConstantinopleOpcodes</c>) — NO inheritance from
        /// a previous fork's table.</para>
        ///
        /// <para><b>Geth ref:</b> <c>core/vm/jump_table.go
        /// newXxxInstructionSet()</c>.</para>
        /// </summary>
        public required OpcodeHandlerTable Opcodes { get; init; }

        // ===================================================================
        // STRATEGY INTERFACES — every fork picks a concrete impl
        // ===================================================================

        /// <summary>
        /// Transaction-type validation rules. Rejects unsupported tx
        /// types (EIP-2930 type-1, EIP-1559 type-2, EIP-4844 type-3,
        /// EIP-7702 type-4) at the active fork with the canonical
        /// geth error tag (e.g. <c>TR_TypeNotSupported</c>).
        ///
        /// <para><b>Common bug:</b> when this is left as
        /// <c>TransactionValidationRules.Empty</c> at a fork that
        /// SHOULD reject a tx type, the EVM silently accepts the tx and
        /// diverges from the canonical "reject + zero gas charged"
        /// state — every fork pre-London must explicitly reject EIP-1559
        /// type-2 transactions, every fork pre-Berlin must reject EIP-2930
        /// type-1.</para>
        ///
        /// <para><b>Geth ref:</b>
        /// <c>core/state_processor.go ApplyTransaction</c> →
        /// per-type validation in <c>core/types/tx_*.go</c>.</para>
        /// </summary>
        public required TransactionValidationRules Validation { get; init; }

        /// <summary>
        /// End-of-transaction cleanup rule for touched-empty accounts
        /// (mirrors geth's
        /// <c>StateDB.Finalise(deleteEmptyObjects:true)</c>).
        ///
        /// <para>Choose <see cref="NoOpTouchedEmptyCleanupRule.Instance"/>
        /// at Frontier through Tangerine Whistle.
        /// Choose <see cref="Eip161TouchedEmptyCleanupRule.Instance"/>
        /// at Spurious Dragon onwards.</para>
        ///
        /// <para><b>Coupling:</b> must agree with
        /// <see cref="EmptyAccountPolicy"/> below — both reflect
        /// EIP-161 activation.</para>
        ///
        /// <para><b>Geth ref:</b> <c>core/state/statedb.go Finalise</c>.</para>
        /// </summary>
        public required ITouchedEmptyCleanupRule TouchedEmptyCleanup { get; init; }

        /// <summary>
        /// SSTORE refund accounting strategy.
        ///
        /// <para><b>Fork history:</b></para>
        /// <list type="bullet">
        ///   <item>Frontier → Byzantium and Petersburg —
        ///   <see cref="LegacySstoreRefundRule.Instance"/>: 15000 refund
        ///   when slot transitions from non-zero to zero. No dirty-slot
        ///   accounting.</item>
        ///   <item>Constantinople (EIP-1283) — <see cref="Eip1283SstoreRefundRule.Instance"/>:
        ///   net-gas metering with per-transition refunds. REVERTED at
        ///   Petersburg due to ChainSecurity reentrancy audit.</item>
        ///   <item>Istanbul (EIP-2200) — same
        ///   <see cref="Eip1283SstoreRefundRule.Instance"/> class but
        ///   the spec also adds the 2300-gas sentry (see
        ///   <see cref="EnforceSstoreSentry"/>).</item>
        ///   <item>Berlin (EIP-2929) — same refund logic; costs change
        ///   via warm/cold tracking in the opcode gas path.</item>
        ///   <item>London (EIP-3529) — clears refund 15000 → 4800;
        ///   refund quotient 2 → 5.</item>
        /// </list>
        ///
        /// <para><b>Geth ref:</b> <c>core/vm/gas_table.go gasSStore</c> /
        /// <c>gasSStoreEIP1283</c> / <c>gasSStoreEIP2200</c> /
        /// <c>gasSStoreEIP2929</c>.</para>
        /// </summary>
        public required ISstoreRefundRule SstoreRefund { get; init; }

        /// <summary>
        /// SELFDESTRUCT semantics — balance transfer + refund + deletion
        /// marking.
        ///
        /// <para><b>Fork history:</b></para>
        /// <list type="bullet">
        ///   <item>Frontier → Tangerine Whistle —
        ///   <see cref="Execution.SelfDestruct.Rules.PreCancunSelfDestructRule.WithRefund24000"/>.</item>
        ///   <item>Spurious Dragon (EIP-161) — same rule, but the
        ///   recipient-empty-account creation cost is gated on
        ///   <c>self.balance &gt; 0</c>.</item>
        ///   <item>London (EIP-3529) — refund eliminated:
        ///   <see cref="Execution.SelfDestruct.Rules.PreCancunSelfDestructRule.WithRefund0"/>.</item>
        ///   <item>Cancun (EIP-6780) — SELFDESTRUCT only destroys when
        ///   called in the same transaction as the creating CREATE;
        ///   otherwise behaves as a balance transfer.</item>
        /// </list>
        ///
        /// <para><b>Common bug:</b> refund-once-per-(tx, contract) must
        /// check a transaction-wide self-destructed set, not a
        /// frame-local list. Frame-local check misses two separate
        /// CALLs SELFDESTRUCTing the same contract.</para>
        ///
        /// <para><b>Geth ref:</b> <c>core/vm/instructions.go opSelfdestruct</c>
        /// + <c>core/vm/gas_table.go gasSelfdestruct</c>.</para>
        /// </summary>
        public required ISelfDestructRule SelfDestruct { get; init; }

        /// <summary>
        /// Gas-forwarding rule for CALL / CREATE / CREATE2 sub-frames.
        ///
        /// <para><b>Fork history:</b></para>
        /// <list type="bullet">
        ///   <item>Frontier → Homestead — <see cref="FullGasForwarding.Instance"/>:
        ///   user-requested gas forwarded as-is. CALL OOGs if it exceeds
        ///   available.</item>
        ///   <item>Tangerine Whistle (EIP-150) — <see cref="Eip150GasForwarding.Instance"/>:
        ///   the 63/64 rule. Forward at most
        ///   <c>gasAvailable - gasAvailable/64</c>.</item>
        /// </list>
        ///
        /// <para><b>Geth ref:</b> <c>core/vm/gas.go callGas</c>.</para>
        /// </summary>
        public required IGasForwardingCalculator GasForwarding { get; init; }

        /// <summary>
        /// CREATE/CREATE2 code-deposit rule — what happens when there
        /// isn't enough gas to pay the per-byte deposit cost.
        ///
        /// <para><b>Fork history:</b></para>
        /// <list type="bullet">
        ///   <item>Frontier — <see cref="FrontierCodeDepositRule.Instance"/>:
        ///   if code-deposit gas insufficient, the CREATE succeeds with
        ///   empty deployed code. Yellow-Paper omission that geth
        ///   reproduces for compatibility.</item>
        ///   <item>Homestead (EIP-2) onwards —
        ///   <see cref="HomesteadCodeDepositRule.Instance"/>: code-deposit
        ///   OOG fails the CREATE, reverts state, consumes all gas.</item>
        /// </list>
        ///
        /// <para><b>Geth ref:</b> <c>core/vm/evm.go create</c> — search
        /// for <c>err == ErrCodeStoreOutOfGas</c>.</para>
        /// </summary>
        public required ICodeDepositRule CodeDeposit { get; init; }

        /// <summary>
        /// CREATE-transaction materialisation rule — commits the empty
        /// contract account when a CREATE-tx has empty init data and zero
        /// value (no init code runs, no value transfers).
        ///
        /// <para><b>Fork history:</b> uniform —
        /// <see cref="MaterialiseEmptyOnSuccessRule.Instance"/> at every fork.
        /// Geth <c>core/vm/evm.go::create</c> calls
        /// <c>StateDB.CreateAccount(addr)</c> unconditionally; the only
        /// per-fork variation is the initial nonce, already encoded by
        /// <c>HardforkConfig.ContractInitialNonce</c>.</para>
        ///
        /// <para><b>Bug it prevents:</b> mainnet block 57,257 — Frontier-era
        /// empty-data CREATE-tx produces a contract account at
        /// keccak256(rlp([sender, nonce]))[12:] that must persist in state
        /// (pre-EIP-161 no cleanup). Without this rule the snapshot in
        /// SetupTargetAccount never commits and the account silently drops
        /// from post-state, breaking the state root.</para>
        /// </summary>
        public required IContractCreationMaterialiseRule ContractCreationMaterialise { get; init; }

        /// <summary>
        /// BLOCKHASH opcode resolution rule.
        ///
        /// <para><b>Fork history:</b></para>
        /// <list type="bullet">
        ///   <item>Frontier → Cancun —
        ///   <see cref="LegacyBlockHashRule.Instance"/>: walk the block
        ///   store via <c>IStateReader.GetBlockHashAsync</c>.</item>
        ///   <item>Prague onwards (EIP-2935) —
        ///   <see cref="Eip2935BlockHashRule.Instance"/>: read storage
        ///   of the history contract.</item>
        /// </list>
        ///
        /// <para><b>Bug it prevents:</b> mainnet block 62,509 — Frontier
        /// lottery contract calls <c>blockhash(N-1) % playerCount</c>.
        /// If BLOCKHASH returns zero (EIP-2935 history contract doesn't
        /// exist pre-Prague), the wrong player is credited, the wrong
        /// post-state results. The legacy path via
        /// <c>StateReader.GetBlockHash</c> reads the canonical header
        /// hash directly.</para>
        ///
        /// <para><b>Geth ref:</b> <c>core/vm/instructions.go opBlockhash</c>
        /// (legacy) / <c>core/vm/contracts.go HistoryStorage</c> (EIP-2935).</para>
        /// </summary>
        public required IBlockHashRule BlockHash { get; init; }

        /// <summary>
        /// Call-frame initialisation rules — run during sub-call frame
        /// setup before the child frame's bytecode begins executing.
        /// Currently used by EIP-7702 to resolve delegation pointers and
        /// charge the delegate-access gas.
        ///
        /// <para><b>Fork history:</b></para>
        /// <list type="bullet">
        ///   <item>Frontier → Shanghai —
        ///   <see cref="CallFrameInitRules.Empty"/>.</item>
        ///   <item>Prague (EIP-7702) — resolves <c>0xef0100‖addr</c>
        ///   delegation bytes and forwards execution to the delegate's
        ///   code.</item>
        /// </list>
        /// </summary>
        public required CallFrameInitRules CallFrameInit { get; init; }

        /// <summary>
        /// Transaction-setup rules that run before the top-level tx is
        /// dispatched to the EVM (access-list pre-warm, beacon block
        /// root precompile, EIP-2935 BlockHash storage, etc.).
        /// </summary>
        public required TransactionSetupRules TransactionSetup { get; init; }

        // ===================================================================
        // NUMERIC CONSTANTS
        // ===================================================================

        /// <summary>
        /// Cap on gas refund as a fraction of gas used:
        /// <c>refund = min(refundCounter, gasUsed / RefundQuotient)</c>.
        ///
        /// <para><b>Fork history:</b></para>
        /// <list type="bullet">
        ///   <item>Frontier → Berlin — <b>2</b> (refund up to 50%).</item>
        ///   <item>London (EIP-3529) — <b>5</b> (refund up to 20%).</item>
        /// </list>
        ///
        /// <para><b>Geth ref:</b> <c>params/protocol_params.go
        /// RefundQuotient</c> / <c>RefundQuotientEIP3529</c>.</para>
        /// </summary>
        public required int RefundQuotient { get; init; }

        /// <summary>
        /// Refund credited when SSTORE clears a non-zero slot to zero.
        ///
        /// <para><b>Fork history:</b></para>
        /// <list type="bullet">
        ///   <item>Frontier → Istanbul — <b>15000</b>.</item>
        ///   <item>London (EIP-3529) — <b>4800</b>.</item>
        /// </list>
        /// </summary>
        public required long SstoreClearsSchedule { get; init; }

        /// <summary>
        /// Refund credited when SSTORE returns a slot to its original
        /// zero value within the same tx (net-gas accounting).
        ///
        /// <para><b>Fork history:</b></para>
        /// <list type="bullet">
        ///   <item>Pre-EIP-1283 / Petersburg — <b>0</b>.</item>
        ///   <item>Constantinople — <b>19800</b> = 20000 − 200.</item>
        ///   <item>Istanbul (EIP-2200) — <b>19200</b> = 20000 − 800.</item>
        ///   <item>Berlin (EIP-2929) — <b>19900</b> = 20000 − 100.</item>
        /// </list>
        /// </summary>
        public required long SstoreSetRefund { get; init; }

        /// <summary>
        /// Refund credited when SSTORE returns a slot to its original
        /// non-zero value within the same tx (net-gas accounting).
        ///
        /// <para><b>Fork history:</b></para>
        /// <list type="bullet">
        ///   <item>Pre-EIP-1283 / Petersburg — <b>0</b>.</item>
        ///   <item>Constantinople — <b>4800</b> = 5000 − 200.</item>
        ///   <item>Istanbul (EIP-2200) — <b>4200</b> = 5000 − 800.</item>
        ///   <item>Berlin (EIP-2929) — <b>2800</b> = 5000 − 2100 − 100.</item>
        /// </list>
        /// </summary>
        public required long SstoreResetRefund { get; init; }

        /// <summary>
        /// Maximum deployed contract code size in bytes.
        ///
        /// <para><b>Fork history:</b></para>
        /// <list type="bullet">
        ///   <item>Frontier → Tangerine Whistle — <b>0</b> (no cap).</item>
        ///   <item>Spurious Dragon (EIP-170) — <b>24576</b> = 0x6000.</item>
        /// </list>
        /// </summary>
        public required int MaxCodeSize { get; init; }

        /// <summary>
        /// Maximum CREATE init-code size in bytes (typically 2 * MaxCodeSize).
        ///
        /// <para><b>Fork history:</b></para>
        /// <list type="bullet">
        ///   <item>Frontier → Paris — <b>0</b> (no cap).</item>
        ///   <item>Shanghai (EIP-3860) — <b>49152</b> = 2 * MaxCodeSize.</item>
        /// </list>
        /// </summary>
        public required int MaxInitCodeSize { get; init; }

        /// <summary>
        /// Maximum number of EIP-4844 blob versioned hashes per block.
        ///
        /// <para><b>Fork history:</b></para>
        /// <list type="bullet">
        ///   <item>Frontier → Shanghai — <b>0</b>.</item>
        ///   <item>Cancun (EIP-4844) — <b>6</b>.</item>
        ///   <item>Prague onwards — <b>9</b>.</item>
        /// </list>
        /// </summary>
        public required int MaxBlobsPerBlock { get; init; }

        /// <summary>
        /// Initial nonce value assigned to a freshly-created contract.
        ///
        /// <para><b>Fork history:</b></para>
        /// <list type="bullet">
        ///   <item>Frontier → Tangerine Whistle — <b>0</b>.</item>
        ///   <item>Spurious Dragon (EIP-161) — <b>1</b> (so a fresh
        ///   contract is never "empty" per EIP-158 definition).</item>
        /// </list>
        /// </summary>
        public required ulong ContractInitialNonce { get; init; }

        // ===================================================================
        // BEHAVIOURAL POLICIES (strategy singletons, not raw bools)
        // ===================================================================

        /// <summary>
        /// EIP-2200 SSTORE 2300-gas reentrancy sentry policy. See
        /// <see cref="SstoreSentryPolicy"/> for fork history.
        /// </summary>
        public required SstoreSentryPolicy EnforceSstoreSentry { get; init; }

        /// <summary>
        /// EIP-3651 coinbase pre-warm policy. See
        /// <see cref="CoinbaseAccessPolicy"/> for fork history.
        /// </summary>
        public required CoinbaseAccessPolicy CoinbaseAccess { get; init; }

        /// <summary>
        /// EIP-1559 base-fee burn policy. See
        /// <see cref="BaseFeePolicy"/> for fork history.
        /// </summary>
        public required BaseFeePolicy BaseFee { get; init; }

        /// <summary>
        /// EIP-161 empty-account cleanup policy. See
        /// <see cref="EmptyAccountPolicy"/> for fork history.
        /// </summary>
        public required EmptyAccountPolicy EmptyAccount { get; init; }

        /// <summary>
        /// EIP-3541 0xEF code prefix rejection policy. See
        /// <see cref="CodePrefixPolicy"/> for fork history.
        /// </summary>
        public required CodePrefixPolicy CodePrefix { get; init; }

        /// <summary>
        /// Per-fork receipt codec used by the receipts-trie, storage layer,
        /// and peer-serving paths. Pre-EIP-2718 forks register
        /// <see cref="LegacyReceiptCodec.Instance"/> (rejects typed
        /// envelope on decode). Berlin onward register
        /// <see cref="Eip2718ReceiptCodec.Instance"/> (dispatches on
        /// <see cref="Receipt.TransactionType"/>).
        /// </summary>
        public required IReceiptCodec ReceiptCodec { get; init; }

        /// <summary>
        /// Per-fork block-header codec used for block-hash computation,
        /// peer-serving, and storage. Each codec encodes its exact field
        /// count (Legacy=15, London=16, Shanghai=17, Cancun=20, Prague=21)
        /// and rejects decode when the wire field count doesn't match.
        /// </summary>
        public required IBlockHeaderCodec HeaderCodec { get; init; }

        /// <summary>
        /// Per-fork transaction decoder. Gates which EIP-2718 envelope
        /// bytes are accepted at this fork: Berlin adds 0x01, London 0x02,
        /// Cancun 0x03, Prague 0x04. Used by body decoders and pooled-tx
        /// handlers to reject txs of types not yet active.
        /// </summary>
        public required ITransactionDecoder TransactionDecoder { get; init; }

        /// <summary>
        /// Per-fork receipt construction rule (EIP-658 gate). Pre-Byzantium
        /// forks register <see cref="PostStateReceiptConstructionRule.Instance"/>
        /// (32-byte intermediate post-state root in the receipt). Byzantium
        /// onward register <see cref="StatusReceiptConstructionRule.Instance"/>
        /// (1-byte status). Honors geth <c>core/state_processor.go:155-159</c>.
        /// </summary>
        public required IReceiptConstructionRule ReceiptConstruction { get; init; }
    }
}
