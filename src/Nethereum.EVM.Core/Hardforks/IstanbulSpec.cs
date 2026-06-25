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
    /// Declarative <see cref="HardforkSpec"/> for the Istanbul hardfork.
    /// Activated on mainnet at block 9,069,000 (2019-12-08).
    ///
    /// <para><b>EIPs activated at Istanbul:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-152 — Blake2 F compression precompile at address 0x09.</item>
    ///   <item>EIP-1108 — alt_bn128 precompile gas reduction (pairing
    ///   100000 + 80000/pair → 45000 + 34000/pair; ecAdd 500 → 150;
    ///   ecMul 40000 → 6000).</item>
    ///   <item>EIP-1344 — CHAINID opcode (0x46).</item>
    ///   <item>EIP-1884 — SLOAD 200 → 800, BALANCE 400 → 700,
    ///   EXTCODEHASH 400 → 700, and new SELFBALANCE opcode (0x47, 5 gas).</item>
    ///   <item>EIP-2028 — calldata non-zero byte cost 68 → 16 (intrinsic).</item>
    ///   <item>EIP-2200 — Net-gas-metering SSTORE refunds with the
    ///   <b>2300-gas reentrancy sentry</b>. This is Istanbul's defining
    ///   policy delta from Constantinople/Petersburg — it was the very
    ///   mitigation that allowed re-enabling EIP-1283 style net-metering
    ///   safely after the ChainSecurity reentrancy audit.</item>
    /// </list>
    ///
    /// <para><b>EIPs explicitly NOT activated at Istanbul:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-2929 (Berlin) — Access-list warm/cold tracking.</item>
    ///   <item>EIP-2930 (Berlin) — Optional access lists / type-1 tx.</item>
    ///   <item>EIP-1559 (London) — Base-fee burn / type-2 tx.</item>
    ///   <item>EIP-3529 (London) — Refund quotient 2 → 5 + clears refund
    ///   15000 → 4800 + SELFDESTRUCT refund removed.</item>
    ///   <item>EIP-3541 (London) — 0xEF code-prefix rejection.</item>
    ///   <item>EIP-3651 (Shanghai) — Warm coinbase pre-warm.</item>
    ///   <item>EIP-6780 (Cancun) — SELFDESTRUCT same-tx-only deletion.</item>
    /// </list>
    ///
    /// <para><b>Spec sources:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-152: <c>https://eips.ethereum.org/EIPS/eip-152</c></item>
    ///   <item>EIP-1108: <c>https://eips.ethereum.org/EIPS/eip-1108</c></item>
    ///   <item>EIP-1344: <c>https://eips.ethereum.org/EIPS/eip-1344</c></item>
    ///   <item>EIP-1884: <c>https://eips.ethereum.org/EIPS/eip-1884</c></item>
    ///   <item>EIP-2028: <c>https://eips.ethereum.org/EIPS/eip-2028</c></item>
    ///   <item>EIP-2200: <c>https://eips.ethereum.org/EIPS/eip-2200</c></item>
    /// </list>
    /// </summary>
    public static class IstanbulSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            // IDENTITY
            Name = HardforkName.Istanbul,

            // OPCODE + INTRINSIC GAS TABLES
            // EIP-2028: calldata non-zero gas 68 → 16.
            // EIP-1344: CHAINID. EIP-1884: SELFBALANCE + SLOAD/BALANCE/EXTCODEHASH repricing.
            // EIP-152: Blake2 precompile 0x09. EIP-1108: alt_bn128 reprice.
            IntrinsicGas = IntrinsicGasRuleSets.Istanbul,
            Opcodes = OpcodeHandlerSets.Istanbul,

            Precompiles = new[]
            {
                new PrecompileSpec { Address = 0x01, Kind = PrecompileKind.Ecrecover },
                new PrecompileSpec { Address = 0x02, Kind = PrecompileKind.Sha256 },
                new PrecompileSpec { Address = 0x03, Kind = PrecompileKind.Ripemd160 },
                new PrecompileSpec { Address = 0x04, Kind = PrecompileKind.Identity },
                new PrecompileSpec { Address = 0x05, Kind = PrecompileKind.ModExp_Eip198 },             // EIP-2565 is Berlin
                new PrecompileSpec { Address = 0x06, Kind = PrecompileKind.Bn256Add_Eip1108 },          // EIP-1108 reprice
                new PrecompileSpec { Address = 0x07, Kind = PrecompileKind.Bn256Mul_Eip1108 },          // EIP-1108
                new PrecompileSpec { Address = 0x08, Kind = PrecompileKind.Bn256Pairing_Eip1108 },      // EIP-1108
                new PrecompileSpec { Address = 0x09, Kind = PrecompileKind.Blake2 },                    // EIP-152
            },

            // STRATEGY INTERFACES
            Validation = TransactionValidationRuleSets.Istanbul,
            TouchedEmptyCleanup = Eip161TouchedEmptyCleanupRule.Instance, // EIP-161 (Spurious Dragon) carries forward
            SstoreRefund = Eip1283SstoreRefundRule.Instance,              // EIP-2200 reuses EIP-1283 net-metering shape (plus sentry below)
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
            SstoreSetRefund = 19200,        // EIP-2200: 20000 - 800 (SLOAD_GAS bumped by EIP-1884)
            SstoreResetRefund = 4200,       // EIP-2200: 5000 - 800
            MaxCodeSize = 24576,            // EIP-170 (Spurious Dragon) 0x6000
            MaxInitCodeSize = 0,            // EIP-3860 not yet (Shanghai)
            MaxBlobsPerBlock = 0,           // EIP-4844 not yet (Cancun)
            ContractInitialNonce = 1,       // EIP-161 (Spurious Dragon)

            // BEHAVIOURAL POLICIES
            EnforceSstoreSentry = SstoreSentryPolicy.Eip2200Active, // EIP-2200: SSTORE 2300-gas reentrancy sentry — Istanbul's headline delta
            CoinbaseAccess = CoinbaseAccessPolicy.Cold,             // EIP-3651 not yet (Shanghai)
            BaseFee = BaseFeePolicy.MinerKeepsAll,                  // EIP-1559 not yet (London)
            EmptyAccount = EmptyAccountPolicy.Eip161Clear,          // EIP-161 (Spurious Dragon)
            CodePrefix = CodePrefixPolicy.Permissive,               // EIP-3541 not yet (London)
            ReceiptCodec = LegacyReceiptCodec.Instance,             // EIP-2718 not yet (Berlin)
            HeaderCodec = LegacyBlockHeaderCodec.Instance,          // 15 fields
            TransactionDecoder = LegacyOnlyTransactionDecoder.Instance,    // pre-EIP-2718
            ReceiptConstruction = StatusReceiptConstructionRule.Instance,  // EIP-658: 1-byte status
        };
    }
}
