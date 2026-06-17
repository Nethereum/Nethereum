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
    /// Declarative <see cref="HardforkSpec"/> for the Constantinople hardfork.
    /// Activated on mainnet at block 7,280,000 (2019-02-28), bundled with
    /// the Petersburg patch which reverted EIP-1283.
    ///
    /// <para><b>EIPs activated at Constantinople:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-145 — Bitwise shifting instructions (SHL, SHR, SAR)
    ///   added to the opcode table.</item>
    ///   <item>EIP-1014 — Skinny CREATE2 opcode (deterministic contract
    ///   addresses).</item>
    ///   <item>EIP-1052 — EXTCODEHASH opcode.</item>
    ///   <item>EIP-1234 — Difficulty-bomb delay + block reward reduced
    ///   from 3 ETH to 2 ETH (consensus-layer only; no EVM impact).</item>
    ///   <item>EIP-1283 — Net-gas-metering SSTORE refunds (sstoreSetRefund
    ///   19800, sstoreResetRefund 4800). REVERTED at Petersburg due to
    ///   ChainSecurity reentrancy audit.</item>
    /// </list>
    ///
    /// <para><b>EIPs explicitly NOT activated at Constantinople:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-2200 (Istanbul) — SSTORE 2300-gas reentrancy sentry.
    ///   <b>Invariant:</b> <see cref="HardforkSpec.EnforceSstoreSentry"/>
    ///   must stay <see cref="SstoreSentryPolicy.Disabled"/> at
    ///   Constantinople. Activating
    ///   <see cref="SstoreSentryPolicy.Eip2200Active"/> here breaks the
    ///   ABCB recursive SSTORE tests because EIP-1283's NOOP path
    ///   intentionally permits SSTORE on as little as 200 gas — the
    ///   sentry was the mitigation Istanbul added later.</item>
    ///   <item>EIP-2929 (Berlin) — Access-list warm/cold tracking.</item>
    ///   <item>EIP-3529 (London) — Refund quotient 2 → 5 + clears refund
    ///   15000 → 4800.</item>
    ///   <item>EIP-3541 (London) — 0xEF code-prefix rejection.</item>
    ///   <item>EIP-3651 (Shanghai) — Warm coinbase pre-warm.</item>
    ///   <item>EIP-1559 (London) — Base-fee burn.</item>
    ///   <item>EIP-6780 (Cancun) — SELFDESTRUCT same-tx-only deletion.</item>
    /// </list>
    ///
    /// <para><b>Spec sources:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-145: <c>https://eips.ethereum.org/EIPS/eip-145</c></item>
    ///   <item>EIP-1014: <c>https://eips.ethereum.org/EIPS/eip-1014</c></item>
    ///   <item>EIP-1052: <c>https://eips.ethereum.org/EIPS/eip-1052</c></item>
    ///   <item>EIP-1234: <c>https://eips.ethereum.org/EIPS/eip-1234</c></item>
    ///   <item>EIP-1283: <c>https://eips.ethereum.org/EIPS/eip-1283</c></item>
    ///   <item>Geth: <c>params/config.go ConstantinopleBlock</c>,
    ///   <c>core/vm/jump_table.go newConstantinopleInstructionSet</c>,
    ///   <c>core/vm/gas_table.go gasSStoreEIP1283</c>.</item>
    /// </list>
    /// </summary>
    public static class ConstantinopleSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            // IDENTITY
            Name = HardforkName.Constantinople,

            // OPCODE + INTRINSIC GAS TABLES (adds SHL/SHR/SAR/CREATE2/EXTCODEHASH)
            IntrinsicGas = IntrinsicGasRuleSets.Constantinople,
            Opcodes = OpcodeHandlerSets.Constantinople,

            Precompiles = new[]
            {
                new PrecompileSpec { Address = 0x01, Kind = PrecompileKind.Ecrecover },
                new PrecompileSpec { Address = 0x02, Kind = PrecompileKind.Sha256 },
                new PrecompileSpec { Address = 0x03, Kind = PrecompileKind.Ripemd160 },
                new PrecompileSpec { Address = 0x04, Kind = PrecompileKind.Identity },
                new PrecompileSpec { Address = 0x05, Kind = PrecompileKind.ModExp_Eip198 },
                new PrecompileSpec { Address = 0x06, Kind = PrecompileKind.Bn256Add_Eip196 },
                new PrecompileSpec { Address = 0x07, Kind = PrecompileKind.Bn256Mul_Eip196 },
                new PrecompileSpec { Address = 0x08, Kind = PrecompileKind.Bn256Pairing_Eip197 },
            },

            // STRATEGY INTERFACES
            Validation = TransactionValidationRuleSets.Constantinople,
            TouchedEmptyCleanup = Eip161TouchedEmptyCleanupRule.Instance, // EIP-161 (Spurious Dragon) carries forward
            SstoreRefund = Eip1283SstoreRefundRule.Instance,              // EIP-1283 net-gas metering (reverted at Petersburg)
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
            SstoreSetRefund = 19800,        // EIP-1283: 20000 - 200 (SLOAD_GAS)
            SstoreResetRefund = 4800,       // EIP-1283: 5000 - 200
            MaxCodeSize = 24576,            // EIP-170 (Spurious Dragon) 0x6000
            MaxInitCodeSize = 0,            // EIP-3860 not yet (Shanghai)
            MaxBlobsPerBlock = 0,           // EIP-4844 not yet (Cancun)
            ContractInitialNonce = 1,       // EIP-161 (Spurious Dragon)

            // BEHAVIOURAL POLICIES
            EnforceSstoreSentry = SstoreSentryPolicy.Disabled, // EIP-2200 (Istanbul) — see class-doc gotcha
            CoinbaseAccess = CoinbaseAccessPolicy.Cold,        // EIP-3651 not yet (Shanghai)
            BaseFee = BaseFeePolicy.MinerKeepsAll,             // EIP-1559 not yet (London)
            EmptyAccount = EmptyAccountPolicy.Eip161Clear,     // EIP-161 (Spurious Dragon)
            CodePrefix = CodePrefixPolicy.Permissive,          // EIP-3541 not yet (London)
        };
    }
}
