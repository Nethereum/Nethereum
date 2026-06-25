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
    /// <b>Frontier</b> — Ethereum genesis fork, block 0 (2015-07-30).
    ///
    /// <para><b>EIPs activated:</b> none — Yellow Paper baseline.</para>
    ///
    /// <para><b>EIPs explicitly NOT activated</b> (gotchas):</para>
    /// <list type="bullet">
    ///   <item>EIP-2 (Homestead) — DELEGATECALL, contract creation cost 53000.</item>
    ///   <item>EIP-7 (Homestead) — DELEGATECALL opcode.</item>
    ///   <item>EIP-150 (Tangerine Whistle) — 63/64 gas forwarding.</item>
    ///   <item>EIP-158/161 (Spurious Dragon) — state clearing.</item>
    ///   <item>EIP-170 (Spurious Dragon) — code size cap.</item>
    ///   <item>Every subsequent EIP.</item>
    /// </list>
    ///
    /// <para><b>Spec sources:</b></para>
    /// <list type="bullet">
    ///   <item>https://ethereum.github.io/yellowpaper/paper.pdf</item>
    /// </list>
    /// </summary>
    public static class FrontierSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            Name = HardforkName.Frontier,

            // Yellow Paper intrinsic gas: 21000 base, 4 zero-byte, 68 non-zero-byte,
            // 32000 contract-creation. No EIP-2028 / EIP-2930 / EIP-3860 / EIP-7623.
            IntrinsicGas = IntrinsicGasRuleSets.Frontier,

            Precompiles = new[]
            {
                new PrecompileSpec { Address = 0x01, Kind = PrecompileKind.Ecrecover },
                new PrecompileSpec { Address = 0x02, Kind = PrecompileKind.Sha256 },
                new PrecompileSpec { Address = 0x03, Kind = PrecompileKind.Ripemd160 },
                new PrecompileSpec { Address = 0x04, Kind = PrecompileKind.Identity },
            },

            // Frontier opcode set per Yellow Paper. No DELEGATECALL (EIP-7),
            // no REVERT (EIP-140), no RETURNDATA* (EIP-211), no SHL/SHR/SAR
            // (EIP-145), no CREATE2 (EIP-1014), no CHAINID/SELFBALANCE
            // (EIP-1344/EIP-1884), no BASEFEE (EIP-3198), no PUSH0 (EIP-3855),
            // no TLOAD/TSTORE/MCOPY (EIP-1153/EIP-5656), no BLOBHASH (EIP-4844).
            Opcodes = OpcodeHandlerSets.Frontier,

            // Pre-EIP-2718: only legacy tx accepted; no per-type rejection logic
            // beyond rejecting unknown serialisations.
            Validation = TransactionValidationRuleSets.Frontier,

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

            // Yellow-Paper quirk: insufficient code-deposit gas leaves the
            // freshly-CREATEd contract with empty deployed code (no error).
            // Homestead (EIP-2) replaces this with a hard failure.
            CodeDeposit = FrontierCodeDepositRule.Instance,
            ContractCreationMaterialise = MaterialiseEmptyOnSuccessRule.Instance,
            BlockHash = LegacyBlockHashRule.Instance,

            // Pre-EIP-7702: no delegation-pointer resolution on call-frame init.
            CallFrameInit = CallFrameInitRules.Empty,

            // Pre-EIP-4788 / EIP-2935 / EIP-2930: no pre-tx system contract
            // calls, no access-list pre-warming.
            TransactionSetup = TransactionSetupRules.Empty,

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

            // Pre-EIP-1283/EIP-2200: SSTORE has no 2300-gas reentrancy sentry.
            EnforceSstoreSentry = SstoreSentryPolicy.Disabled,

            // Pre-EIP-3651: coinbase is cold by default — first access pays the
            // full cold-account cost.
            CoinbaseAccess = CoinbaseAccessPolicy.Cold,

            // Pre-EIP-1559: miner keeps the full gas-price; no base-fee burn.
            BaseFee = BaseFeePolicy.MinerKeepsAll,

            // Pre-EIP-161: empty accounts persist; touches do not delete them.
            EmptyAccount = EmptyAccountPolicy.Persist,

            // Pre-EIP-3541: 0xEF code prefix is allowed in deployed code.
            CodePrefix = CodePrefixPolicy.Permissive,
            ReceiptCodec = LegacyReceiptCodec.Instance,        // pre-EIP-658: PostStateOrStatus carries 32-byte intermediate state root
            HeaderCodec = LegacyBlockHeaderCodec.Instance,     // 15 fields (Yellow Paper)
            TransactionDecoder = LegacyOnlyTransactionDecoder.Instance,    // pre-EIP-2718
            ReceiptConstruction = PostStateReceiptConstructionRule.Instance, // pre-EIP-658: 32-byte intermediate state root
        };
    }
}
