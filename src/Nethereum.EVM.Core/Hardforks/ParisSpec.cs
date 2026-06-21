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
    /// Declarative <see cref="HardforkSpec"/> for the Paris hardfork —
    /// a.k.a. <b>The Merge</b> (mainnet block 15,537,394 — 2022-09-15).
    ///
    /// <para><b>Nature of the upgrade:</b> Paris is overwhelmingly a
    /// <i>consensus-layer</i> change: Ethereum switched from
    /// proof-of-work to proof-of-stake. From the execution-layer / EVM
    /// perspective the only observable changes are opcode semantics for
    /// the former DIFFICULTY (<c>0x44</c>) slot and the disappearance of
    /// the block reward. Gas rules, refund schedules, code limits and
    /// transaction validation are <b>identical to London</b>.</para>
    ///
    /// <para><b>EIPs activated at Paris:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-3675 — Upgrade consensus to proof-of-stake. The
    ///   <c>DIFFICULTY</c> opcode at <c>0x44</c> is renamed to
    ///   <c>PREVRANDAO</c>; block reward is removed (no more PoW
    ///   issuance to the coinbase).</item>
    ///   <item>EIP-4399 — Supplant DIFFICULTY semantics: opcode
    ///   <c>0x44</c> now returns the beacon-chain RANDAO mix for the
    ///   prior block instead of the (now non-existent) PoW difficulty.
    ///   The opcode mnemonic becomes <c>PREVRANDAO</c>.</item>
    /// </list>
    ///
    /// <para><b>EIPs explicitly NOT activated at Paris:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-3651 (Coinbase warm) — Shanghai.</item>
    ///   <item>EIP-3855 (PUSH0) — Shanghai.</item>
    ///   <item>EIP-3860 (Init-code cap) — Shanghai.</item>
    ///   <item>EIP-4895 (Beacon withdrawals) — Shanghai.</item>
    ///   <item>EIP-1153 / EIP-4844 / EIP-5656 / EIP-6780 / EIP-7516 — Cancun.</item>
    /// </list>
    ///
    /// <para><b>Spec sources:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-3675: <c>https://eips.ethereum.org/EIPS/eip-3675</c></item>
    ///   <item>EIP-4399: <c>https://eips.ethereum.org/EIPS/eip-4399</c></item>
    ///   <item>execution-specs <c>network-upgrades/mainnet-upgrades/paris.md</c>.</item>
    ///   <item>Geth: <c>params/config.go MergeNetsplitBlock</c>,
    ///   <c>core/vm/jump_table.go newMergeInstructionSet</c>.</item>
    /// </list>
    ///
    /// <para><b>Gotcha:</b> the opcode value <c>0x44</c> stays the same
    /// and the gas cost stays at 2 — only the semantics of what is
    /// returned changes. Any test that exercises <c>0x44</c> at Paris
    /// must source the value from the block's <c>prevRandao</c> field,
    /// not from a difficulty value.</para>
    /// </summary>
    public static class ParisSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            // IDENTITY
            Name = HardforkName.Paris,

            // OPCODE + INTRINSIC GAS TABLES — Paris reuses the London
            // tables; only the semantics of opcode 0x44 (now PREVRANDAO)
            // change, which is wired in the opcode handler itself.
            IntrinsicGas = IntrinsicGasRuleSets.Paris,
            Opcodes = OpcodeHandlerSets.Paris,

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

            // STRATEGY INTERFACES — all identical to London.
            Validation = TransactionValidationRules.Empty,            // No new tx types at Paris (type-0/1/2 accepted as at London)
            TouchedEmptyCleanup = Eip161TouchedEmptyCleanupRule.Instance, // EIP-161 carries forward
            SstoreRefund = Eip1283SstoreRefundRule.Instance,             // Same refund logic since Istanbul; London-rebased values below
            SelfDestruct = PreCancunSelfDestructRule.WithRefund0,        // EIP-3529 (London) eliminated the 24000 refund
            GasForwarding = Eip150GasForwarding.Instance,                // 63/64 rule from Tangerine Whistle
            CodeDeposit = HomesteadCodeDepositRule.Instance,             // OOG-on-code-deposit failure (Homestead+)
            ContractCreationMaterialise = MaterialiseEmptyOnSuccessRule.Instance,
            BlockHash = LegacyBlockHashRule.Instance,
            CallFrameInit = CallFrameInitRules.Empty,                    // No EIP-7702 yet (Prague)
            TransactionSetup = TransactionSetupRules.Empty,              // No EIP-4788/EIP-2935 setup yet (Cancun/Prague)

            // NUMERIC CONSTANTS — identical to London.
            RefundQuotient = 5,             // EIP-3529 (London): refund up to 20% of gas used
            SstoreClearsSchedule = 4800,    // EIP-3529 (London): 15000 -> 4800
            SstoreSetRefund = 19900,        // EIP-2929 (Berlin): 20000 - 100 (warm SLOAD)
            SstoreResetRefund = 2800,       // EIP-2929 (Berlin): 5000 - 2100 - 100
            MaxCodeSize = 24576,            // EIP-170 (Spurious Dragon) 0x6000
            MaxInitCodeSize = 0,            // EIP-3860 not yet (Shanghai)
            MaxBlobsPerBlock = 0,           // EIP-4844 not yet (Cancun)
            ContractInitialNonce = 1,       // EIP-161 (Spurious Dragon)

            // BEHAVIOURAL POLICIES — identical to London.
            EnforceSstoreSentry = SstoreSentryPolicy.Eip2200Active,     // EIP-2200 sentry from Istanbul
            CoinbaseAccess = CoinbaseAccessPolicy.Cold,                 // EIP-3651 not yet (Shanghai) — coinbase still cold
            BaseFee = BaseFeePolicy.Eip1559Burnt,                       // EIP-1559 (London) — base fee burnt, priority fee to proposer
            EmptyAccount = EmptyAccountPolicy.Eip161Clear,              // EIP-161 (Spurious Dragon)
            CodePrefix = CodePrefixPolicy.Eip3541RejectEf,              // EIP-3541 (London) — reject deployed code starting with 0xEF
            ReceiptCodec = Eip2718ReceiptCodec.Instance,                // typed receipts
            HeaderCodec = LondonBlockHeaderCodec.Instance,              // 16 fields (Merge doesn't add a header field)
            TransactionDecoder = Eip1559TransactionDecoder.Instance,       // Merge doesn't add a tx type
            ReceiptConstruction = StatusReceiptConstructionRule.Instance,  // EIP-658: 1-byte status
        };
    }
}
