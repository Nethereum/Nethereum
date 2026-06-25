using Nethereum.EVM.Execution.Opcodes.Executors.Rules;
using Nethereum.EVM.Execution.CallFrame;
using Nethereum.EVM.Execution.Create.Rules;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.EVM.Execution.SelfDestruct.Rules;
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
    /// Declarative <see cref="HardforkSpec"/> for the Tangerine Whistle hardfork.
    /// Activated on mainnet at block 2,463,000 (2016-10-18). The anti-DoS
    /// response to the Shanghai-attack spam transactions that exploited
    /// underpriced state-access opcodes.
    ///
    /// <para><b>EIPs activated at Tangerine Whistle:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-150 — Gas cost changes for IO-heavy operations
    ///   (EXTCODESIZE/EXTCODECOPY/BALANCE/SLOAD/CALL/CALLCODE/DELEGATECALL/
    ///   SELFDESTRUCT bumped to 400/700 etc.) AND the 63/64 sub-call gas
    ///   forwarding rule
    ///   (<c>forwarded = min(requested, available - available/64)</c>).</item>
    /// </list>
    ///
    /// <para><b>EIPs explicitly NOT activated at Tangerine Whistle</b> (gotchas):</para>
    /// <list type="bullet">
    ///   <item>EIP-155 (Spurious Dragon) — chain-id replay protection.</item>
    ///   <item>EIP-158/161 (Spurious Dragon) — empty-account state clearing.
    ///   Tangerine Whistle is the LAST fork at which touched-empty accounts
    ///   persist; <see cref="EmptyAccountPolicy.Persist"/> remains.</item>
    ///   <item>EIP-170 (Spurious Dragon) — 24576 byte code-size cap.
    ///   <see cref="HardforkSpec.MaxCodeSize"/> stays 0.</item>
    ///   <item>EIP-140 / EIP-198 / EIP-211 / EIP-214 (Byzantium) — REVERT,
    ///   modexp, RETURNDATA*, STATICCALL.</item>
    ///   <item>Every subsequent EIP.</item>
    /// </list>
    ///
    /// <para><b>Validation ruleset:</b>
    /// <see cref="TransactionValidationRuleSets.Frontier"/> is shared because
    /// both Frontier and Tangerine Whistle reject every typed transaction
    /// (no EIP-2718 yet) — the ruleset name reflects shape, not fork-of-origin.</para>
    ///
    /// <para><b>Spec sources:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-150: <c>https://eips.ethereum.org/EIPS/eip-150</c></item>
    /// </list>
    /// </summary>
    public static class TangerineWhistleSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            // IDENTITY
            Name = HardforkName.TangerineWhistle,

            // OPCODE + INTRINSIC GAS TABLES (EIP-150 state-access cost bumps)
            IntrinsicGas = IntrinsicGasRuleSets.TangerineWhistle,
            Opcodes = OpcodeHandlerSets.TangerineWhistle,

            // INTRINSIC GAS CONSTANTS (same as Homestead — EIP-150 doesn't change intrinsic gas)

            // PRECOMPILES (0x01–0x04, no new ones at Tangerine)
            Precompiles = new[]
            {
                new PrecompileSpec { Address = 0x01, Kind = PrecompileKind.Ecrecover },
                new PrecompileSpec { Address = 0x02, Kind = PrecompileKind.Sha256 },
                new PrecompileSpec { Address = 0x03, Kind = PrecompileKind.Ripemd160 },
                new PrecompileSpec { Address = 0x04, Kind = PrecompileKind.Identity },
            },

            // STRATEGY INTERFACES
            // Frontier ruleset shared — both reject all typed transactions (pre-EIP-2718).
            Validation = TransactionValidationRuleSets.Frontier,
            TouchedEmptyCleanup = NoOpTouchedEmptyCleanupRule.Instance,   // Pre-EIP-161
            SstoreRefund = LegacySstoreRefundRule.Instance,               // Pre-EIP-1283: clears-to-zero only (15000)
            SelfDestruct = PreCancunSelfDestructRule.WithRefund24000,     // 24000 refund still active
            GasForwarding = Eip150GasForwarding.Instance,                 // EIP-150: 63/64 rule
            CodeDeposit = HomesteadCodeDepositRule.Instance,              // Homestead (EIP-2) OOG-on-code-deposit failure carries forward
            ContractCreationMaterialise = MaterialiseEmptyOnSuccessRule.Instance,
            BlockHash = LegacyBlockHashRule.Instance,
            CallFrameInit = CallFrameInitRules.Empty,                     // No EIP-7702 yet
            TransactionSetup = TransactionSetupRules.Empty,               // No EIP-4788/EIP-2935/EIP-2930 setup yet

            // NUMERIC CONSTANTS
            RefundQuotient = 2,             // Pre-EIP-3529
            SstoreClearsSchedule = 15000,   // Pre-EIP-3529
            SstoreSetRefund = 0,            // Pre-EIP-1283/EIP-2200
            SstoreResetRefund = 0,          // Pre-EIP-1283/EIP-2200
            MaxCodeSize = 0,                // EIP-170 not yet (Spurious Dragon)
            MaxInitCodeSize = 0,            // EIP-3860 not yet (Shanghai)
            MaxBlobsPerBlock = 0,           // EIP-4844 not yet (Cancun)
            ContractInitialNonce = 0,       // EIP-161 not yet (Spurious Dragon)

            // BEHAVIOURAL POLICIES
            EnforceSstoreSentry = SstoreSentryPolicy.Disabled, // EIP-2200 not yet (Istanbul)
            CoinbaseAccess = CoinbaseAccessPolicy.Cold,        // EIP-3651 not yet (Shanghai)
            BaseFee = BaseFeePolicy.MinerKeepsAll,             // EIP-1559 not yet (London)
            EmptyAccount = EmptyAccountPolicy.Persist,         // EIP-161 not yet (Spurious Dragon)
            CodePrefix = CodePrefixPolicy.Permissive,          // EIP-3541 not yet (London)
            ReceiptCodec = LegacyReceiptCodec.Instance,        // pre-EIP-658
            HeaderCodec = LegacyBlockHeaderCodec.Instance,     // 15 fields
            TransactionDecoder = LegacyOnlyTransactionDecoder.Instance,    // pre-EIP-2718
            ReceiptConstruction = PostStateReceiptConstructionRule.Instance, // pre-EIP-658: 32-byte intermediate state root
        };
    }
}
