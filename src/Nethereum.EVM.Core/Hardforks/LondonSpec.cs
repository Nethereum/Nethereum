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
    /// Declarative <see cref="HardforkSpec"/> for the London hardfork
    /// (mainnet block 12,965,000 — 2021-08-05).
    ///
    /// <para><b>EIPs activated at London:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-1559 — Fee market reform. Introduces base fee (burnt)
    ///   + priority tip (to miner) and the type-2 dynamic-fee transaction
    ///   envelope. <b>London's signature change.</b></item>
    ///   <item>EIP-3198 — BASEFEE opcode (0x48) exposing the current
    ///   block's base fee to the EVM.</item>
    ///   <item>EIP-3529 — Refund reduction. Refund quotient 2 → 5 (max
    ///   refund drops from 50% to 20% of gas used); SSTORE clears
    ///   schedule 15000 → 4800; SELFDESTRUCT refund eliminated (24000 → 0).</item>
    ///   <item>EIP-3541 — Reject new deployed code starting with the
    ///   0xEF byte (reserves the prefix for future EOF format).</item>
    ///   <item>EIP-3554 — Difficulty bomb delay to December 2021
    ///   (consensus-only — no EVM effect, recorded for completeness).</item>
    /// </list>
    ///
    /// <para><b>EIPs explicitly NOT activated at London:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-3651 (Shanghai) — Coinbase pre-warm. London leaves
    ///   coinbase cold: first BALANCE/EXTCODE*/CALL to coinbase still
    ///   pays 2600.</item>
    ///   <item>EIP-3855 / EIP-3860 (Shanghai) — PUSH0 opcode and
    ///   init-code cap.</item>
    ///   <item>EIP-4844 / EIP-1153 / EIP-5656 / EIP-6780 / EIP-7516
    ///   (Cancun) — Blobs, transient storage, MCOPY, SELFDESTRUCT
    ///   same-tx-only, BLOBBASEFEE.</item>
    ///   <item>EIP-4788 (Cancun) — Beacon block root precompile.</item>
    /// </list>
    ///
    /// <para><b>Spec sources:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-1559: <c>https://eips.ethereum.org/EIPS/eip-1559</c></item>
    ///   <item>EIP-3198: <c>https://eips.ethereum.org/EIPS/eip-3198</c></item>
    ///   <item>EIP-3529: <c>https://eips.ethereum.org/EIPS/eip-3529</c></item>
    ///   <item>EIP-3541: <c>https://eips.ethereum.org/EIPS/eip-3541</c></item>
    /// </list>
    ///
    /// <para><b>Gotcha:</b> London accepts legacy (type-0), EIP-2930
    /// (type-1) and EIP-1559 (type-2) transactions — no type rejection
    /// at this fork, so <see cref="TransactionValidationRules.Empty"/>
    /// is the correct choice.</para>
    /// </summary>
    public static class LondonSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            // IDENTITY
            Name = HardforkName.London,

            // OPCODE + INTRINSIC GAS TABLES (EIP-3198 BASEFEE opcode
            // added; intrinsic gas unchanged from Berlin — access-list
            // pricing still applies via EIP-2930).
            IntrinsicGas = IntrinsicGasRuleSets.London,
            Opcodes = OpcodeHandlerSets.London,

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

            // STRATEGY INTERFACES
            // London accepts type-0/1/2 — no transaction-type rejection.
            Validation = TransactionValidationRules.Empty,
            TouchedEmptyCleanup = Eip161TouchedEmptyCleanupRule.Instance, // EIP-161 carries forward
            SstoreRefund = Eip1283SstoreRefundRule.Instance,              // EIP-2200 net-gas refund logic; clears value comes from SstoreClearsSchedule
            SelfDestruct = PreCancunSelfDestructRule.WithRefund0,         // EIP-3529: SELFDESTRUCT refund eliminated (24000 → 0)
            GasForwarding = Eip150GasForwarding.Instance,                 // 63/64 rule from Tangerine Whistle
            CodeDeposit = HomesteadCodeDepositRule.Instance,              // OOG-on-code-deposit failure (Homestead+)
            ContractCreationMaterialise = MaterialiseEmptyOnSuccessRule.Instance,
            BlockHash = LegacyBlockHashRule.Instance,
            CallFrameInit = CallFrameInitRules.Empty,                     // No EIP-7702 yet
            TransactionSetup = TransactionSetupRules.Empty,               // No EIP-4788/EIP-2935 setup yet

            // NUMERIC CONSTANTS
            RefundQuotient = 5,             // EIP-3529: refund up to 20% of gas used (was 2 = 50% at Berlin)
            SstoreClearsSchedule = 4800,    // EIP-3529: 15000 → 4800
            SstoreSetRefund = 19900,        // EIP-2929 (Berlin): 20000 - 100 (warm SLOAD)
            SstoreResetRefund = 2800,       // EIP-2929 (Berlin): 5000 - 2100 - 100
            MaxCodeSize = 24576,            // EIP-170 (Spurious Dragon) 0x6000
            MaxInitCodeSize = 0,            // EIP-3860 not yet (Shanghai)
            MaxBlobsPerBlock = 0,           // EIP-4844 not yet (Cancun)
            ContractInitialNonce = 1,       // EIP-161 (Spurious Dragon)

            // BEHAVIOURAL POLICIES
            EnforceSstoreSentry = SstoreSentryPolicy.Eip2200Active, // EIP-2200 sentry from Istanbul carries forward
            CoinbaseAccess = CoinbaseAccessPolicy.Cold,             // EIP-3651 not yet (Shanghai) — coinbase still cold
            BaseFee = BaseFeePolicy.Eip1559Burnt,                   // EIP-1559: base fee burnt, priority tip to miner
            EmptyAccount = EmptyAccountPolicy.Eip161Clear,          // EIP-161 (Spurious Dragon)
            CodePrefix = CodePrefixPolicy.Eip3541RejectEf,          // EIP-3541: reject deployed code starting with 0xEF
            ReceiptCodec = Eip2718ReceiptCodec.Instance,            // typed receipts: 0x01 (AccessList) + 0x02 (DynamicFee)
            HeaderCodec = LondonBlockHeaderCodec.Instance,          // 16 fields (+baseFee EIP-1559)
            TransactionDecoder = Eip1559TransactionDecoder.Instance,       // adds 0x02 dynamic fee
            ReceiptConstruction = StatusReceiptConstructionRule.Instance,  // EIP-658: 1-byte status
        };
    }
}
