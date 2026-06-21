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
    /// Declarative <see cref="HardforkSpec"/> for the Byzantium hardfork.
    /// Activated on mainnet at block 4,370,000 (2017-10-16) as the first
    /// phase of the Metropolis upgrade.
    ///
    /// <para><b>EIPs activated at Byzantium:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-100 — Difficulty adjustment accounting for uncles
    ///   (consensus-layer only; no EVM impact).</item>
    ///   <item>EIP-140 — REVERT opcode (0xFD): reverts state changes but
    ///   returns data and refunds remaining gas.</item>
    ///   <item>EIP-196 — Precompiled contracts for addition + scalar
    ///   multiplication on the alt_bn128 elliptic curve (0x06, 0x07).</item>
    ///   <item>EIP-197 — Precompiled contract for optimal Ate pairing
    ///   check on alt_bn128 (0x08), enabling zkSNARK verification.</item>
    ///   <item>EIP-198 — Precompiled contract for big-integer modular
    ///   exponentiation (0x05), enabling RSA verification.</item>
    ///   <item>EIP-211 — RETURNDATASIZE (0x3D) + RETURNDATACOPY (0x3E)
    ///   opcodes for inspecting child-frame return data.</item>
    ///   <item>EIP-214 — STATICCALL opcode (0xFA): non-state-modifying
    ///   sub-call (disallows SSTORE, CREATE, SELFDESTRUCT, LOGn, CALL
    ///   with value).</item>
    ///   <item>EIP-649 — Difficulty-bomb delay + block reward reduced
    ///   from 5 ETH to 3 ETH (consensus-layer only; no EVM impact).</item>
    /// </list>
    ///
    /// <para><b>Carried forward from Spurious Dragon:</b> EIP-161 state
    /// clearing, EIP-170 24576-byte code-size cap, EIP-160 EXP cost
    /// repricing, EIP-155 chain-id replay protection.</para>
    ///
    /// <para><b>EIPs explicitly NOT activated at Byzantium:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-145 (Constantinople) — SHL/SHR/SAR bitwise shifts.</item>
    ///   <item>EIP-1014 (Constantinople) — CREATE2 deterministic addresses.</item>
    ///   <item>EIP-1052 (Constantinople) — EXTCODEHASH.</item>
    ///   <item>EIP-1283 (Constantinople) — Net-gas-metering SSTORE refunds.</item>
    ///   <item>EIP-2200 (Istanbul) — SSTORE 2300-gas reentrancy sentry.</item>
    ///   <item>EIP-2929 (Berlin) — Access-list warm/cold tracking.</item>
    ///   <item>EIP-3529 (London) — Refund quotient 2 → 5 + clears refund 15000 → 4800.</item>
    ///   <item>EIP-3541 (London) — 0xEF code-prefix rejection.</item>
    ///   <item>EIP-3651 (Shanghai) — Warm coinbase pre-warm.</item>
    ///   <item>EIP-1559 (London) — Base-fee burn.</item>
    ///   <item>EIP-6780 (Cancun) — SELFDESTRUCT same-tx-only deletion.</item>
    /// </list>
    ///
    /// <para><b>Spec sources:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-100: <c>https://eips.ethereum.org/EIPS/eip-100</c></item>
    ///   <item>EIP-140: <c>https://eips.ethereum.org/EIPS/eip-140</c></item>
    ///   <item>EIP-196: <c>https://eips.ethereum.org/EIPS/eip-196</c></item>
    ///   <item>EIP-197: <c>https://eips.ethereum.org/EIPS/eip-197</c></item>
    ///   <item>EIP-198: <c>https://eips.ethereum.org/EIPS/eip-198</c></item>
    ///   <item>EIP-211: <c>https://eips.ethereum.org/EIPS/eip-211</c></item>
    ///   <item>EIP-214: <c>https://eips.ethereum.org/EIPS/eip-214</c></item>
    ///   <item>EIP-649: <c>https://eips.ethereum.org/EIPS/eip-649</c></item>
    ///   <item>Geth: <c>params/config.go ByzantiumBlock</c>,
    ///   <c>core/vm/jump_table.go newByzantiumInstructionSet</c>.</item>
    /// </list>
    /// </summary>
    public static class ByzantiumSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            // IDENTITY
            Name = HardforkName.Byzantium,

            // OPCODE + INTRINSIC GAS TABLES
            // Adds REVERT, RETURNDATASIZE, RETURNDATACOPY, STATICCALL +
            // modexp/alt_bn128 precompiles. Pre-EIP-145/1014/1052.
            IntrinsicGas = IntrinsicGasRuleSets.Byzantium,
            Opcodes = OpcodeHandlerSets.Byzantium,

            Precompiles = new[]
            {
                new PrecompileSpec { Address = 0x01, Kind = PrecompileKind.Ecrecover },
                new PrecompileSpec { Address = 0x02, Kind = PrecompileKind.Sha256 },
                new PrecompileSpec { Address = 0x03, Kind = PrecompileKind.Ripemd160 },
                new PrecompileSpec { Address = 0x04, Kind = PrecompileKind.Identity },
                new PrecompileSpec { Address = 0x05, Kind = PrecompileKind.ModExp_Eip198 },          // EIP-198
                new PrecompileSpec { Address = 0x06, Kind = PrecompileKind.Bn256Add_Eip196 },        // EIP-196
                new PrecompileSpec { Address = 0x07, Kind = PrecompileKind.Bn256Mul_Eip196 },        // EIP-196
                new PrecompileSpec { Address = 0x08, Kind = PrecompileKind.Bn256Pairing_Eip197 },    // EIP-197
            },

            // STRATEGY INTERFACES
            Validation = TransactionValidationRuleSets.Byzantium,
            TouchedEmptyCleanup = Eip161TouchedEmptyCleanupRule.Instance, // EIP-161 (Spurious Dragon) carries forward
            SstoreRefund = LegacySstoreRefundRule.Instance,               // Pre-EIP-1283: clears-to-zero only, no net-gas metering
            SelfDestruct = PreCancunSelfDestructRule.WithRefund24000,     // 24000 refund still active (removed at London/EIP-3529)
            GasForwarding = Eip150GasForwarding.Instance,                 // 63/64 rule from Tangerine Whistle
            CodeDeposit = HomesteadCodeDepositRule.Instance,              // OOG-on-code-deposit failure (Homestead+)
            ContractCreationMaterialise = MaterialiseEmptyOnSuccessRule.Instance,
            BlockHash = LegacyBlockHashRule.Instance,
            CallFrameInit = CallFrameInitRules.Empty,                     // No EIP-7702 yet
            TransactionSetup = TransactionSetupRules.Empty,               // No EIP-4788/EIP-2935 setup yet

            // NUMERIC CONSTANTS
            RefundQuotient = 2,             // Pre-EIP-3529 (refund up to 50% of gas used)
            SstoreClearsSchedule = 15000,   // Pre-EIP-3529
            SstoreSetRefund = 0,            // Pre-EIP-1283: no net-gas refund on set-to-original-zero
            SstoreResetRefund = 0,          // Pre-EIP-1283: no net-gas refund on reset-to-original-nonzero
            MaxCodeSize = 24576,            // EIP-170 (Spurious Dragon) 0x6000
            MaxInitCodeSize = 0,            // EIP-3860 not yet (Shanghai)
            MaxBlobsPerBlock = 0,           // EIP-4844 not yet (Cancun)
            ContractInitialNonce = 1,       // EIP-161 (Spurious Dragon)

            // BEHAVIOURAL POLICIES
            EnforceSstoreSentry = SstoreSentryPolicy.Disabled, // EIP-2200 not yet (Istanbul)
            CoinbaseAccess = CoinbaseAccessPolicy.Cold,        // EIP-3651 not yet (Shanghai)
            BaseFee = BaseFeePolicy.MinerKeepsAll,             // EIP-1559 not yet (London)
            EmptyAccount = EmptyAccountPolicy.Eip161Clear,     // EIP-161 (Spurious Dragon)
            CodePrefix = CodePrefixPolicy.Permissive,          // EIP-3541 not yet (London)
            ReceiptCodec = LegacyReceiptCodec.Instance,        // EIP-658 status active but typed envelope (EIP-2718) not yet — Berlin
            HeaderCodec = LegacyBlockHeaderCodec.Instance,     // 15 fields (no baseFee until London)
            TransactionDecoder = LegacyOnlyTransactionDecoder.Instance,    // pre-EIP-2718
            ReceiptConstruction = StatusReceiptConstructionRule.Instance,  // EIP-658: 1-byte status (Byzantium activation)
        };
    }
}
