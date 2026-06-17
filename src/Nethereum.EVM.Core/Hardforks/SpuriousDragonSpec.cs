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

namespace Nethereum.EVM.Hardforks
{
    /// <summary>
    /// Declarative <see cref="HardforkSpec"/> for the Spurious Dragon hardfork.
    /// Activated on mainnet at block 2,675,000 (2016-11-22) as the second
    /// anti-DoS hardfork following Tangerine Whistle. This fork is the
    /// EIP-161 state-clearing boundary — a major policy change in how the
    /// EVM treats empty accounts.
    ///
    /// <para><b>EIPs activated at Spurious Dragon:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-155 — Simple replay protection: chain ID folded into
    ///   the transaction signature so signed txs from one chain cannot
    ///   replay on another. Consensus / signing-layer change; no EVM
    ///   opcode impact.</item>
    ///   <item>EIP-160 — EXP gas cost bump: per-byte cost raised from
    ///   10 to 50 to price the opcode in line with its real cost.</item>
    ///   <item>EIP-161 — State trie clearing: touched-empty accounts are
    ///   deleted at end-of-tx, and freshly-created contracts start with
    ///   nonce 1 (so a brand-new contract is never "empty" per the
    ///   EIP-158 definition). Eliminates the empty-account DoS attack
    ///   accumulated by the Shanghai-attack tx spam.</item>
    ///   <item>EIP-170 — Max contract code size 24576 bytes (0x6000).
    ///   Prevents quadratic-in-code-size DoS at JUMPDEST analysis.</item>
    /// </list>
    ///
    /// <para><b>EIPs explicitly NOT activated at Spurious Dragon:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-140 (Byzantium) — REVERT opcode.</item>
    ///   <item>EIP-211 (Byzantium) — RETURNDATASIZE / RETURNDATACOPY.</item>
    ///   <item>EIP-214 (Byzantium) — STATICCALL.</item>
    ///   <item>EIP-145 (Constantinople) — SHL / SHR / SAR.</item>
    ///   <item>EIP-1014 (Constantinople) — CREATE2.</item>
    ///   <item>EIP-1052 (Constantinople) — EXTCODEHASH.</item>
    ///   <item>EIP-1283 / EIP-2200 — Net-gas-metering SSTORE refunds.</item>
    ///   <item>EIP-2929 (Berlin) — Access-list warm/cold tracking.</item>
    ///   <item>EIP-3529 (London) — Refund quotient bump + clears refund
    ///   reduction.</item>
    ///   <item>EIP-1559 (London) — Base-fee burn.</item>
    ///   <item>EIP-3541 (London) — 0xEF code-prefix rejection.</item>
    ///   <item>EIP-3651 (Shanghai) — Warm coinbase.</item>
    ///   <item>EIP-6780 (Cancun) — SELFDESTRUCT same-tx-only deletion.</item>
    /// </list>
    ///
    /// <para><b>Spec sources:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-155: <c>https://eips.ethereum.org/EIPS/eip-155</c></item>
    ///   <item>EIP-160: <c>https://eips.ethereum.org/EIPS/eip-160</c></item>
    ///   <item>EIP-161: <c>https://eips.ethereum.org/EIPS/eip-161</c></item>
    ///   <item>EIP-170: <c>https://eips.ethereum.org/EIPS/eip-170</c></item>
    ///   <item>Geth: <c>params/config.go EIP158Block</c>,
    ///   <c>core/vm/jump_table.go newSpuriousDragonInstructionSet</c>,
    ///   <c>core/state/statedb.go Finalise (deleteEmptyObjects)</c>.</item>
    /// </list>
    /// </summary>
    public static class SpuriousDragonSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            // IDENTITY
            Name = HardforkName.SpuriousDragon,

            // OPCODE + INTRINSIC GAS TABLES (EIP-160 EXP cost bump applies)
            IntrinsicGas = IntrinsicGasRuleSets.SpuriousDragon,
            Opcodes = OpcodeHandlerSets.SpuriousDragon,

            // INTRINSIC GAS CONSTANTS (same as Tangerine — EIP-155/160/161/170 change opcode/state semantics, not intrinsic gas)

            // PRECOMPILES (0x01–0x04, unchanged from Tangerine)
            Precompiles = new[]
            {
                new PrecompileSpec { Address = 0x01, Kind = PrecompileKind.Ecrecover },
                new PrecompileSpec { Address = 0x02, Kind = PrecompileKind.Sha256 },
                new PrecompileSpec { Address = 0x03, Kind = PrecompileKind.Ripemd160 },
                new PrecompileSpec { Address = 0x04, Kind = PrecompileKind.Identity },
            },

            // STRATEGY INTERFACES
            Validation = TransactionValidationRuleSets.Frontier,           // No type-1/2/3/4 txs yet (EIP-2718 is Berlin)
            TouchedEmptyCleanup = Eip161TouchedEmptyCleanupRule.Instance,  // EIP-161 activates here
            SstoreRefund = LegacySstoreRefundRule.Instance,                // Pre-EIP-1283: clears-to-zero refund only
            SelfDestruct = PreCancunSelfDestructRule.WithRefund24000,      // 24000 refund still active (removed at London/EIP-3529)
            GasForwarding = Eip150GasForwarding.Instance,                  // 63/64 rule carries forward from Tangerine Whistle
            CodeDeposit = HomesteadCodeDepositRule.Instance,               // OOG-on-code-deposit failure (Homestead+)
            ContractCreationMaterialise = MaterialiseEmptyOnSuccessRule.Instance,
            BlockHash = LegacyBlockHashRule.Instance,
            CallFrameInit = CallFrameInitRules.Empty,                      // No EIP-7702 yet
            TransactionSetup = TransactionSetupRules.Empty,                // No EIP-4788 / EIP-2935 / access-list pre-warm yet

            // NUMERIC CONSTANTS
            RefundQuotient = 2,             // Pre-EIP-3529 (refund up to 50% of gas used)
            SstoreClearsSchedule = 15000,   // Pre-EIP-3529
            SstoreSetRefund = 0,            // Pre-EIP-1283/EIP-2200: no net-gas refund
            SstoreResetRefund = 0,          // Pre-EIP-1283/EIP-2200: no net-gas refund
            MaxCodeSize = 24576,            // EIP-170: 0x6000
            MaxInitCodeSize = 0,            // EIP-3860 not yet (Shanghai)
            MaxBlobsPerBlock = 0,           // EIP-4844 not yet (Cancun)
            ContractInitialNonce = 1,       // EIP-161: fresh contract is never "empty"

            // BEHAVIOURAL POLICIES
            EnforceSstoreSentry = SstoreSentryPolicy.Disabled,  // EIP-2200 not yet (Istanbul)
            CoinbaseAccess = CoinbaseAccessPolicy.Cold,         // EIP-3651 not yet (Shanghai)
            BaseFee = BaseFeePolicy.MinerKeepsAll,              // EIP-1559 not yet (London)
            EmptyAccount = EmptyAccountPolicy.Eip161Clear,      // EIP-161 activates here
            CodePrefix = CodePrefixPolicy.Permissive,           // EIP-3541 not yet (London)
        };
    }
}
