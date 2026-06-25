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
    /// <b>Homestead</b> — first planned hardfork, block 1,150,000 (2016-03-14).
    ///
    /// <para><b>EIPs activated at Homestead:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-2 — Contract creation cost lifted 21000 → 53000;
    ///   DIFFICULTY adjustment; CREATE OOG on insufficient code-deposit
    ///   gas now fails the CREATE (was silent empty-code deploy at Frontier).</item>
    ///   <item>EIP-7 — DELEGATECALL opcode (0xF4).</item>
    ///   <item>EIP-8 — devp2p forwards compatibility (network-layer, no EVM impact).</item>
    /// </list>
    ///
    /// <para><b>EIPs explicitly NOT activated at Homestead:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-150 (Tangerine Whistle) — 63/64 gas forwarding rule.</item>
    ///   <item>EIP-155 (Spurious Dragon) — chain-id replay protection.</item>
    ///   <item>EIP-158/161 (Spurious Dragon) — state clearing / empty-account deletion.</item>
    ///   <item>EIP-170 (Spurious Dragon) — code size cap.</item>
    ///   <item>Every subsequent EIP.</item>
    /// </list>
    ///
    /// <para><b>Spec sources:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-2: <c>https://eips.ethereum.org/EIPS/eip-2</c></item>
    ///   <item>EIP-7: <c>https://eips.ethereum.org/EIPS/eip-7</c></item>
    /// </list>
    /// </summary>
    public static class HomesteadSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            // IDENTITY
            Name = HardforkName.Homestead,

            // OPCODE + INTRINSIC GAS TABLES
            // EIP-2: contract-creation intrinsic cost 21000 → 53000.
            IntrinsicGas = IntrinsicGasRuleSets.Homestead,
            Precompiles = new[]
            {
                new PrecompileSpec { Address = 0x01, Kind = PrecompileKind.Ecrecover },
                new PrecompileSpec { Address = 0x02, Kind = PrecompileKind.Sha256 },
                new PrecompileSpec { Address = 0x03, Kind = PrecompileKind.Ripemd160 },
                new PrecompileSpec { Address = 0x04, Kind = PrecompileKind.Identity },
            },
            // EIP-7: DELEGATECALL (0xF4) added to the dispatch table.
            Opcodes = OpcodeHandlerSets.Homestead,

            // STRATEGY INTERFACES
            Validation = TransactionValidationRuleSets.Homestead,

            // Pre-EIP-161: touched-empty accounts persist; no end-of-tx cleanup.
            TouchedEmptyCleanup = NoOpTouchedEmptyCleanupRule.Instance,

            // Pre-EIP-1283/EIP-2200: clears-to-zero refund only (15000), no
            // dirty-slot net-gas accounting.
            SstoreRefund = LegacySstoreRefundRule.Instance,

            // Pre-EIP-3529: SELFDESTRUCT credits 24000 refund.
            // Pre-EIP-6780: SELFDESTRUCT always destroys.
            SelfDestruct = PreCancunSelfDestructRule.WithRefund24000,

            // Pre-EIP-150: user-requested call gas forwarded as-is, no 63/64 cap.
            GasForwarding = FullGasForwarding.Instance,

            // EIP-2: insufficient code-deposit gas now hard-fails the CREATE
            // (consumes all gas, reverts state). Replaces Frontier's silent
            // empty-code deploy.
            CodeDeposit = HomesteadCodeDepositRule.Instance,
            ContractCreationMaterialise = MaterialiseEmptyOnSuccessRule.Instance,
            BlockHash = LegacyBlockHashRule.Instance,

            // Pre-EIP-7702: no delegation-pointer resolution on call-frame init.
            CallFrameInit = CallFrameInitRules.Empty,

            // Pre-EIP-4788 / EIP-2935 / EIP-2930: no pre-tx system contract
            // calls, no access-list pre-warming.
            TransactionSetup = TransactionSetupRules.Empty,

            // NUMERIC CONSTANTS
            // Pre-EIP-3529: refund capped at gasUsed / 2 (i.e. up to 50%).
            RefundQuotient = 2,

            // Pre-EIP-3529: 15000 refund when SSTORE clears non-zero → zero.
            SstoreClearsSchedule = 15000,

            // Pre-EIP-1283/EIP-2200: no net-gas refund on set-to-original-zero.
            SstoreSetRefund = 0,

            // Pre-EIP-1283/EIP-2200: no net-gas refund on reset-to-original-nonzero.
            SstoreResetRefund = 0,

            // Pre-EIP-170: no contract-code size cap.
            MaxCodeSize = 0,

            // Pre-EIP-3860: no CREATE init-code size cap.
            MaxInitCodeSize = 0,

            // Pre-EIP-4844: no blob transactions.
            MaxBlobsPerBlock = 0,

            // Pre-EIP-161: freshly-created contracts start with nonce 0.
            // Spurious Dragon bumps this to 1 so a new contract is never "empty".
            ContractInitialNonce = 0,

            // BEHAVIOURAL POLICIES
            // Pre-EIP-1283/EIP-2200: SSTORE has no 2300-gas reentrancy sentry.
            EnforceSstoreSentry = SstoreSentryPolicy.Disabled,

            // Pre-EIP-3651: coinbase is cold by default.
            CoinbaseAccess = CoinbaseAccessPolicy.Cold,

            // Pre-EIP-1559: miner keeps the full gas-price; no base-fee burn.
            BaseFee = BaseFeePolicy.MinerKeepsAll,

            // Pre-EIP-161: empty accounts persist; touches do not delete them.
            EmptyAccount = EmptyAccountPolicy.Persist,

            // Pre-EIP-3541: 0xEF code prefix is allowed in deployed code.
            CodePrefix = CodePrefixPolicy.Permissive,
            ReceiptCodec = LegacyReceiptCodec.Instance,        // pre-EIP-658
            HeaderCodec = LegacyBlockHeaderCodec.Instance,     // 15 fields
            TransactionDecoder = LegacyOnlyTransactionDecoder.Instance,    // pre-EIP-2718
            ReceiptConstruction = PostStateReceiptConstructionRule.Instance, // pre-EIP-658: 32-byte intermediate state root
        };
    }
}
