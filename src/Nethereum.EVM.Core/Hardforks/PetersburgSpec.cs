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
    /// Declarative <see cref="HardforkSpec"/> for the Petersburg hardfork
    /// (a.k.a. ConstantinopleFix, EIP-1716).
    ///
    /// <para><b>EMERGENCY REVERT.</b> Petersburg activated on mainnet at
    /// the SAME block as Constantinople (7,280,000, 2019-02-28) and
    /// exists solely to revert EIP-1283. The ChainSecurity reentrancy
    /// audit (2019-01-15) demonstrated that EIP-1283's net-gas SSTORE
    /// metering allowed a reentrancy attack pattern where a contract
    /// could perform a state-modifying SSTORE in a callback consuming
    /// only the dirty-slot cost (200 gas), bypassing the implicit
    /// 2300-gas reentrancy guard that <c>transfer()</c> / <c>send()</c>
    /// callers had been relying on for years. The Ethereum Foundation
    /// delayed Constantinople by ~3 weeks and shipped Petersburg
    /// (EIP-1716) to drop EIP-1283 before any mainnet block executed
    /// under the buggy rules. The 2300-gas sentry was reintroduced
    /// properly via EIP-2200 at Istanbul.</para>
    ///
    /// <para><b>EIPs active at Petersburg:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-145 — Bitwise shifting instructions (SHL, SHR, SAR).</item>
    ///   <item>EIP-1014 — Skinny CREATE2 opcode.</item>
    ///   <item>EIP-1052 — EXTCODEHASH opcode.</item>
    ///   <item>EIP-1234 — Difficulty-bomb delay + block reward 3 → 2 ETH
    ///   (consensus-layer only; no EVM impact).</item>
    /// </list>
    ///
    /// <para><b>EIP DELIBERATELY OMITTED at Petersburg:</b></para>
    /// <list type="bullet">
    ///   <item><b>EIP-1283 — REVERTED.</b> SSTORE goes back to the
    ///   pre-Constantinople legacy model:
    ///   <see cref="LegacySstoreRefundRule.Instance"/>, 15000 refund on
    ///   non-zero → zero only, no dirty-slot accounting,
    ///   <c>SstoreSetRefund = 0</c>, <c>SstoreResetRefund = 0</c>.
    ///   This is the single behavioural difference from
    ///   <see cref="ConstantinopleSpec"/>.</item>
    /// </list>
    ///
    /// <para><b>Spec sources:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-1716 (Petersburg meta): <c>https://eips.ethereum.org/EIPS/eip-1716</c></item>
    ///   <item>ChainSecurity audit:
    ///   <c>https://chainsecurity.com/constantinople-enables-new-reentrancy-attack/</c></item>
    /// </list>
    /// </summary>
    public static class PetersburgSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            // IDENTITY
            Name = HardforkName.Petersburg,

            // OPCODE + INTRINSIC GAS TABLES (same as Constantinople — only SSTORE refund logic differs)
            IntrinsicGas = IntrinsicGasRuleSets.Petersburg,
            Opcodes = OpcodeHandlerSets.Petersburg,

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
            Validation = TransactionValidationRuleSets.Constantinople,    // Same as Constantinople (both reject type-1/2)
            TouchedEmptyCleanup = Eip161TouchedEmptyCleanupRule.Instance, // EIP-161 (Spurious Dragon) carries forward
            SstoreRefund = LegacySstoreRefundRule.Instance,               // EIP-1283 REVERTED — back to legacy (this is the Petersburg difference)
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
            SstoreSetRefund = 0,            // EIP-1283 reverted — legacy model has no net-gas set refund
            SstoreResetRefund = 0,          // EIP-1283 reverted — legacy model has no net-gas reset refund
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
            ReceiptCodec = LegacyReceiptCodec.Instance,        // EIP-2718 not yet (Berlin)
            HeaderCodec = LegacyBlockHeaderCodec.Instance,     // 15 fields
            TransactionDecoder = LegacyOnlyTransactionDecoder.Instance,    // pre-EIP-2718
            ReceiptConstruction = StatusReceiptConstructionRule.Instance,  // EIP-658: 1-byte status
        };
    }
}
