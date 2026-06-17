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
    /// Declarative <see cref="HardforkSpec"/> for the Berlin hardfork
    /// (mainnet block 12,244,000 — 2021-04-15).
    ///
    /// <para><b>EIPs activated at Berlin:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-2565 — ModExp precompile gas cost reduction
    ///   (recalibrated formula at precompile <c>0x05</c>).</item>
    ///   <item>EIP-2718 — Typed transaction envelope (allows future
    ///   transaction types via a leading type byte).</item>
    ///   <item>EIP-2929 — Cold / warm storage and account access cost
    ///   split. <b>Berlin's signature change:</b> first access in a tx
    ///   pays 2600 (account) / 2100 (storage slot); subsequent accesses
    ///   pay 100 (warm). SSTORE reset cost rebased to 5000 − 2100 − 100
    ///   = 2800 refund, set refund to 20000 − 100 = 19900.</item>
    ///   <item>EIP-2930 — Optional access list (type-1 transaction).
    ///   Pre-warms addresses and storage slots; charges 2400 per address
    ///   + 1900 per storage key in the intrinsic gas.</item>
    /// </list>
    ///
    /// <para><b>EIPs explicitly NOT activated at Berlin:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-1559 (London) — Base-fee burn / dynamic-fee tx type-2.
    ///   Berlin still pays the full gas fee to the miner.</item>
    ///   <item>EIP-3198 (London) — BASEFEE opcode.</item>
    ///   <item>EIP-3529 (London) — Refund quotient 2 → 5 and clears
    ///   refund 15000 → 4800. Berlin keeps the legacy 50% refund cap
    ///   and the 15000 clears refund.</item>
    ///   <item>EIP-3541 (London) — 0xEF code-prefix rejection.</item>
    ///   <item>EIP-3651 (Shanghai) — Coinbase pre-warm (still cold at
    ///   Berlin: first BALANCE/EXTCODE*/CALL to coinbase pays 2600).</item>
    ///   <item>EIP-3855 / EIP-3860 (Shanghai) — PUSH0 opcode and
    ///   init-code cap.</item>
    ///   <item>EIP-6780 (Cancun) — SELFDESTRUCT same-tx-only deletion.</item>
    /// </list>
    ///
    /// <para><b>Spec sources:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-2565: <c>https://eips.ethereum.org/EIPS/eip-2565</c></item>
    ///   <item>EIP-2718: <c>https://eips.ethereum.org/EIPS/eip-2718</c></item>
    ///   <item>EIP-2929: <c>https://eips.ethereum.org/EIPS/eip-2929</c></item>
    ///   <item>EIP-2930: <c>https://eips.ethereum.org/EIPS/eip-2930</c></item>
    ///   <item>Geth: <c>params/config.go BerlinBlock</c>,
    ///   <c>core/vm/jump_table.go newBerlinInstructionSet</c>,
    ///   <c>core/vm/gas_table.go gasSStoreEIP2929</c>.</item>
    /// </list>
    ///
    /// <para><b>Gotcha:</b> Berlin accepts type-1 (EIP-2930) but MUST
    /// reject type-2 (EIP-1559) — that arrives at London.
    /// <see cref="TransactionValidationRuleSets.Berlin"/> uses
    /// <c>PreLondonTxTypeRule.BerlinOnly</c> for this distinction.</para>
    /// </summary>
    public static class BerlinSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            // IDENTITY
            Name = HardforkName.Berlin,

            // OPCODE + INTRINSIC GAS TABLES (EIP-2929 cold/warm in opcode
            // gas costs; EIP-2930 access-list entries in intrinsic gas).
            IntrinsicGas = IntrinsicGasRuleSets.Berlin,
            Opcodes = OpcodeHandlerSets.Berlin,

            Precompiles = new[]
            {
                new PrecompileSpec { Address = 0x01, Kind = PrecompileKind.Ecrecover },
                new PrecompileSpec { Address = 0x02, Kind = PrecompileKind.Sha256 },
                new PrecompileSpec { Address = 0x03, Kind = PrecompileKind.Ripemd160 },
                new PrecompileSpec { Address = 0x04, Kind = PrecompileKind.Identity },
                new PrecompileSpec { Address = 0x05, Kind = PrecompileKind.ModExp_Eip2565 },           // EIP-2565 reprice
                new PrecompileSpec { Address = 0x06, Kind = PrecompileKind.Bn256Add_Eip1108 },
                new PrecompileSpec { Address = 0x07, Kind = PrecompileKind.Bn256Mul_Eip1108 },
                new PrecompileSpec { Address = 0x08, Kind = PrecompileKind.Bn256Pairing_Eip1108 },
                new PrecompileSpec { Address = 0x09, Kind = PrecompileKind.Blake2 },
            },

            // STRATEGY INTERFACES
            // EIP-2718 type-1 accepted; EIP-1559 type-2 rejected.
            Validation = TransactionValidationRuleSets.Berlin,
            TouchedEmptyCleanup = Eip161TouchedEmptyCleanupRule.Instance, // EIP-161 carries forward
            SstoreRefund = Eip1283SstoreRefundRule.Instance,              // Same refund logic as Istanbul; costs change via warm/cold
            SelfDestruct = PreCancunSelfDestructRule.WithRefund24000,     // 24000 refund still active (eliminated at London/EIP-3529)
            GasForwarding = Eip150GasForwarding.Instance,                 // 63/64 rule from Tangerine Whistle
            CodeDeposit = HomesteadCodeDepositRule.Instance,              // OOG-on-code-deposit failure (Homestead+)
            ContractCreationMaterialise = MaterialiseEmptyOnSuccessRule.Instance,
            BlockHash = LegacyBlockHashRule.Instance,
            CallFrameInit = CallFrameInitRules.Empty,                     // No EIP-7702 yet
            TransactionSetup = TransactionSetupRules.Empty,               // No EIP-4788/EIP-2935 setup yet

            // NUMERIC CONSTANTS
            RefundQuotient = 2,             // Pre-EIP-3529 (refund up to 50% of gas used)
            SstoreClearsSchedule = 15000,   // Pre-EIP-3529 (London drops to 4800)
            SstoreSetRefund = 19900,        // EIP-2929: 20000 - 100 (warm SLOAD)
            SstoreResetRefund = 2800,       // EIP-2929: 5000 - 2100 - 100 (cold SLOAD removed, warm SLOAD)
            MaxCodeSize = 24576,            // EIP-170 (Spurious Dragon) 0x6000
            MaxInitCodeSize = 0,            // EIP-3860 not yet (Shanghai)
            MaxBlobsPerBlock = 0,           // EIP-4844 not yet (Cancun)
            ContractInitialNonce = 1,       // EIP-161 (Spurious Dragon)

            // BEHAVIOURAL POLICIES
            EnforceSstoreSentry = SstoreSentryPolicy.Eip2200Active, // EIP-2200 sentry from Istanbul carries forward
            CoinbaseAccess = CoinbaseAccessPolicy.Cold,             // EIP-3651 not yet (Shanghai) — coinbase still cold
            BaseFee = BaseFeePolicy.MinerKeepsAll,                  // EIP-1559 not yet (London) — miner keeps full fee
            EmptyAccount = EmptyAccountPolicy.Eip161Clear,          // EIP-161 (Spurious Dragon)
            CodePrefix = CodePrefixPolicy.Permissive,               // EIP-3541 not yet (London) — 0xEF code allowed
        };
    }
}
