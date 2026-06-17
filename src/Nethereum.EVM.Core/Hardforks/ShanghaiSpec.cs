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
    /// Declarative <see cref="HardforkSpec"/> for the Shanghai hardfork
    /// (mainnet block 17,034,870 — 2023-04-12).
    ///
    /// <para><b>EIPs activated at Shanghai:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-3651 — Warm COINBASE. The block's coinbase address is
    ///   pre-added to the access list so the first BALANCE / EXTCODE* /
    ///   CALL targeting it pays the warm 100-gas cost instead of the cold
    ///   2600. <b>Shanghai's signature EVM change.</b></item>
    ///   <item>EIP-3855 — PUSH0 opcode (0x5F) pushes the constant zero
    ///   onto the stack for 2 gas (base).</item>
    ///   <item>EIP-3860 — Limit and meter init code. Caps CREATE/CREATE2
    ///   init code at 49152 bytes (2 * MaxCodeSize) and charges 2 gas per
    ///   32-byte init-code word at intrinsic gas time.</item>
    ///   <item>EIP-4895 — Beacon chain push withdrawals. Validator
    ///   withdrawals are credited to recipient accounts by the consensus
    ///   layer. <i>No EVM effect</i> — recorded here for completeness;
    ///   handled outside the EVM execution path.</item>
    /// </list>
    ///
    /// <para><b>EIPs explicitly NOT activated at Shanghai:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-1153 / EIP-4844 / EIP-4788 / EIP-5656 / EIP-6780 /
    ///   EIP-7516 (Cancun) — Transient storage, blob transactions,
    ///   beacon-block-root precompile, MCOPY, SELFDESTRUCT same-tx-only,
    ///   BLOBBASEFEE.</item>
    ///   <item>EIP-2537 / EIP-2935 / EIP-7702 / EIP-7623 / EIP-7691
    ///   (Prague) — BLS precompiles, historical BLOCKHASH storage,
    ///   set-EOA-code type-4 tx, calldata floor cost, blob throughput
    ///   increase.</item>
    /// </list>
    ///
    /// <para><b>Spec sources:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-3651: <c>https://eips.ethereum.org/EIPS/eip-3651</c></item>
    ///   <item>EIP-3855: <c>https://eips.ethereum.org/EIPS/eip-3855</c></item>
    ///   <item>EIP-3860: <c>https://eips.ethereum.org/EIPS/eip-3860</c></item>
    ///   <item>EIP-4895: <c>https://eips.ethereum.org/EIPS/eip-4895</c></item>
    ///   <item>execution-specs <c>network-upgrades/mainnet-upgrades/shanghai.md</c>.</item>
    ///   <item>Geth: <c>params/config.go ShanghaiTime</c>,
    ///   <c>core/vm/jump_table.go newShanghaiInstructionSet</c>.</item>
    /// </list>
    ///
    /// <para><b>Gotcha:</b> Shanghai accepts legacy (type-0), EIP-2930
    /// (type-1) and EIP-1559 (type-2) transactions — no type rejection at
    /// this fork, so <see cref="TransactionValidationRules.Empty"/> is the
    /// correct choice. EIP-4895 withdrawals are a block-level credit
    /// applied by consensus, never seen by the EVM dispatch loop.</para>
    /// </summary>
    public static class ShanghaiSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            // IDENTITY
            Name = HardforkName.Shanghai,

            // OPCODE + INTRINSIC GAS TABLES
            // EIP-3855 adds PUSH0 to the opcode table; EIP-3860 adds the
            // 2-gas-per-32-byte init-code word charge at intrinsic time.
            IntrinsicGas = IntrinsicGasRuleSets.Shanghai,
            Opcodes = OpcodeHandlerSets.Shanghai,

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
            // Shanghai accepts type-0/1/2 — no transaction-type rejection.
            Validation = TransactionValidationRules.Empty,
            TouchedEmptyCleanup = Eip161TouchedEmptyCleanupRule.Instance, // EIP-161 carries forward
            SstoreRefund = Eip1283SstoreRefundRule.Instance,              // EIP-2200 net-gas refund logic; clears value from SstoreClearsSchedule
            SelfDestruct = PreCancunSelfDestructRule.WithRefund0,         // EIP-3529 (London): SELFDESTRUCT refund eliminated
            GasForwarding = Eip150GasForwarding.Instance,                 // 63/64 rule from Tangerine Whistle
            CodeDeposit = HomesteadCodeDepositRule.Instance,              // OOG-on-code-deposit failure (Homestead+)
            ContractCreationMaterialise = MaterialiseEmptyOnSuccessRule.Instance,
            BlockHash = LegacyBlockHashRule.Instance,
            CallFrameInit = CallFrameInitRules.Empty,                     // No EIP-7702 yet (Prague)
            TransactionSetup = TransactionSetupRules.Empty,               // No EIP-4788/EIP-2935 setup yet (Cancun/Prague)

            // NUMERIC CONSTANTS
            RefundQuotient = 5,             // EIP-3529 (London): refund up to 20% of gas used
            SstoreClearsSchedule = 4800,    // EIP-3529 (London): 15000 → 4800
            SstoreSetRefund = 19900,        // EIP-2929 (Berlin): 20000 - 100 (warm SLOAD)
            SstoreResetRefund = 2800,       // EIP-2929 (Berlin): 5000 - 2100 - 100
            MaxCodeSize = 24576,            // EIP-170 (Spurious Dragon) 0x6000
            MaxInitCodeSize = 49152,        // EIP-3860 (Shanghai): 2 * MaxCodeSize
            MaxBlobsPerBlock = 0,           // EIP-4844 not yet (Cancun)
            ContractInitialNonce = 1,       // EIP-161 (Spurious Dragon)

            // BEHAVIOURAL POLICIES
            EnforceSstoreSentry = SstoreSentryPolicy.Eip2200Active, // EIP-2200 (Istanbul) sentry carries forward
            CoinbaseAccess = CoinbaseAccessPolicy.Eip3651Warm,      // EIP-3651 (Shanghai): coinbase pre-warmed
            BaseFee = BaseFeePolicy.Eip1559Burnt,                   // EIP-1559 (London): base fee burnt
            EmptyAccount = EmptyAccountPolicy.Eip161Clear,          // EIP-161 (Spurious Dragon)
            CodePrefix = CodePrefixPolicy.Eip3541RejectEf,          // EIP-3541 (London): reject 0xEF-prefixed deployed code
        };
    }
}
