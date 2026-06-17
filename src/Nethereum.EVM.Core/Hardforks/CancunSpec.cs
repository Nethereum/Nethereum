using Nethereum.EVM.Execution.Opcodes.Executors.Rules;
using Nethereum.EVM.Execution.CallFrame;
using Nethereum.EVM.Execution.Create;
using Nethereum.EVM.Execution.Create.Rules;
using Nethereum.EVM.Execution.Opcodes;
using Nethereum.EVM.Execution.SelfDestruct;
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
    /// Declarative <see cref="HardforkSpec"/> for the Cancun hardfork
    /// (mainnet block 19,426,587 — 2024-03-13).
    ///
    /// <para><b>EIPs activated at Cancun:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-1153 — Transient storage opcodes (TLOAD / TSTORE).</item>
    ///   <item>EIP-4788 — Beacon block root precompile at
    ///   <c>0x000F3df6D732807Ef1319fB7B8bB8522d0Beac02</c>, written each
    ///   block by the system transaction sequence.</item>
    ///   <item>EIP-4844 — Shard-blob transactions (type-3), KZG point
    ///   evaluation precompile at <c>0x0A</c>, BLOBHASH opcode,
    ///   versioned-hash validation, max 6 blobs per block.</item>
    ///   <item>EIP-5656 — MCOPY opcode for efficient memory-to-memory copy.</item>
    ///   <item>EIP-6780 — SELFDESTRUCT only destroys the contract when
    ///   invoked in the same transaction as the contract's CREATE; otherwise
    ///   it behaves as a plain balance transfer.</item>
    ///   <item>EIP-7516 — BLOBBASEFEE opcode exposing the current block's
    ///   blob base fee.</item>
    /// </list>
    ///
    /// <para><b>EIPs explicitly NOT activated at Cancun:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-2537 (BLS12-381 precompiles) — Prague.</item>
    ///   <item>EIP-2935 (Historical BLOCKHASH storage contract) — Prague.</item>
    ///   <item>EIP-7702 (Set EOA account code via type-4 tx) — Prague.</item>
    ///   <item>EIP-7623 (Increase calldata floor cost) — Prague.</item>
    ///   <item>EIP-7691 (Blob throughput increase to 9) — Prague.</item>
    ///   <item>EIP-7251 / EIP-7002 / EIP-6110 consensus-layer requests — Prague.</item>
    ///   <item>EIP-7594 (PeerDAS) — Osaka.</item>
    ///   <item>EIP-7825 (Tx gas limit cap) — Osaka.</item>
    /// </list>
    ///
    /// <para><b>Spec sources:</b>
    /// <list type="bullet">
    ///   <item>execution-specs <c>network-upgrades/mainnet-upgrades/cancun.md</c>.</item>
    ///   <item>geth <c>params/config.go CancunTime</c>, <c>core/vm/jump_table.go newCancunInstructionSet</c>.</item>
    ///   <item>EELS <c>src/ethereum/cancun/__init__.py</c>.</item>
    /// </list>
    /// </para>
    ///
    /// <para><b>Status:</b> Cancun is the canary fork for this codebase —
    /// any spec or rule change must keep Cancun at full pass before
    /// touching other forks.</para>
    /// </summary>
    public static class CancunSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            Name = HardforkName.Cancun,

            // Opcode + intrinsic gas tables — Cancun adds TLOAD/TSTORE
            // (EIP-1153), MCOPY (EIP-5656), BLOBHASH (EIP-4844),
            // BLOBBASEFEE (EIP-7516) over the Shanghai table.
            IntrinsicGas = IntrinsicGasRuleSets.Cancun,
            Opcodes = OpcodeHandlerSets.Cancun,

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
                new PrecompileSpec { Address = 0x0A, Kind = PrecompileKind.PointEvaluation },
            },

            // EIP-4844: validate type-3 blob transactions (versioned-hash
            // commitment count, KZG commitments, blob gas limit).
            Validation = TransactionValidationRuleSets.Cancun,

            // EIP-161 finalisation (Spurious Dragon onwards) — touched
            // empty accounts are deleted at end-of-tx.
            TouchedEmptyCleanup = Eip161TouchedEmptyCleanupRule.Instance,

            // EIP-2200 net-gas SSTORE refunds (Istanbul) — clears refund
            // value comes from SstoreClearsSchedule (4800 at London+).
            SstoreRefund = Eip1283SstoreRefundRule.Instance,

            // EIP-6780: SELFDESTRUCT only deletes the contract when it
            // was CREATEd in the same transaction; otherwise balance is
            // transferred but contract code/storage persist. Refund is 0
            // (eliminated at London / EIP-3529).
            SelfDestruct = Eip6780SelfDestructRule.Instance,

            // EIP-150 63/64ths gas-forwarding rule (Tangerine Whistle).
            GasForwarding = Eip150GasForwarding.Instance,

            // EIP-2 code-deposit OOG fails the CREATE (Homestead onwards).
            CodeDeposit = HomesteadCodeDepositRule.Instance,
            ContractCreationMaterialise = MaterialiseEmptyOnSuccessRule.Instance,
            BlockHash = LegacyBlockHashRule.Instance,

            // EIP-4788 beacon-block-root precompile + access-list pre-warm
            // (the Cancun setup rules also pre-warm the coinbase per
            // EIP-3651 inherited from Shanghai).
            CallFrameInit = CallFrameInitRuleSets.Cancun,
            TransactionSetup = TransactionSetupRuleSets.Cancun,

            // EIP-3529 (London): refund quotient 5 (max 20% of gasUsed),
            // SSTORE clears refund 4800.
            RefundQuotient = 5,
            SstoreClearsSchedule = 4800,

            // EIP-2929 (Berlin) cold/warm accounting carried through:
            // set-refund 20000 - 100 = 19900, reset-refund 5000 - 2100 - 100 = 2800.
            SstoreSetRefund = 19900,
            SstoreResetRefund = 2800,

            // EIP-170 (Spurious Dragon) 24,576-byte deployed-code cap.
            MaxCodeSize = 24576,

            // EIP-3860 (Shanghai) 2 * MaxCodeSize init-code cap.
            MaxInitCodeSize = 49152,

            // EIP-4844 6 blobs per block (target 3, max 6 at Cancun;
            // raised to 9 at Prague via EIP-7691).
            MaxBlobsPerBlock = 6,

            // EIP-161 (Spurious Dragon) — freshly-created contracts start
            // at nonce 1 so they are never "empty" by EIP-158 definition.
            ContractInitialNonce = 1,

            // EIP-2200 (Istanbul) 2300-gas SSTORE reentrancy sentry.
            EnforceSstoreSentry = SstoreSentryPolicy.Eip2200Active,

            // EIP-3651 (Shanghai) — coinbase pre-warmed in access list.
            CoinbaseAccess = CoinbaseAccessPolicy.Eip3651Warm,

            // EIP-1559 (London) — base fee burnt, priority fee to miner.
            BaseFee = BaseFeePolicy.Eip1559Burnt,

            // EIP-161 (Spurious Dragon) — empty accounts deleted on touch.
            EmptyAccount = EmptyAccountPolicy.Eip161Clear,

            // EIP-3541 (London) — reject deployed code starting with 0xEF
            // (reserves the prefix for future EOF format).
            CodePrefix = CodePrefixPolicy.Eip3541RejectEf,
        };
    }
}
