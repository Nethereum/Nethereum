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
using Nethereum.Model.Codecs;

namespace Nethereum.EVM.Hardforks
{
    /// <summary>
    /// Declarative <see cref="HardforkSpec"/> for the Prague-Electra hardfork
    /// ("Pectra" — mainnet activation 2025-05-07).
    ///
    /// <para><b>EIPs activated at Prague:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-2537 — BLS12-381 curve operations precompiles at
    ///   addresses <c>0x0B</c>–<c>0x11</c> (G1 add/mul/multi-exp,
    ///   G2 add/mul/multi-exp, pairing, map-to-curve).</item>
    ///   <item>EIP-2935 — Serve historical block hashes from a system
    ///   storage contract at
    ///   <c>0x0000F90827F1C53a10cb7A02335B175320002935</c>, written each
    ///   block by the system transaction sequence; BLOCKHASH reads from
    ///   the contract for the 8192-block window.</item>
    ///   <item>EIP-6110 — Validator deposits surfaced as execution-layer
    ///   requests (deposit contract events become consensus-visible).</item>
    ///   <item>EIP-7002 — Validator triggerable withdrawals/exits from
    ///   the execution layer (system contract at the withdrawal
    ///   request predeploy address).</item>
    ///   <item>EIP-7251 — Increase the MAX_EFFECTIVE_BALANCE for
    ///   validators (consensus-layer; execution-layer change is the
    ///   consolidation request system contract).</item>
    ///   <item>EIP-7549 — Move committee index outside attestation
    ///   (consensus-only; no EVM impact).</item>
    ///   <item>EIP-7623 — Increase calldata floor cost. Charges a
    ///   minimum of <c>10 * tokens_in_calldata</c> gas regardless of
    ///   execution to guarantee min tx cost and discourage calldata
    ///   spam blobs.</item>
    ///   <item>EIP-7685 — General-purpose execution-layer requests:
    ///   bundles deposits / withdrawals / consolidations into a
    ///   single requests root committed to the block header.</item>
    ///   <item>EIP-7691 — Blob throughput increase: target 6 / max 9
    ///   blobs per block (up from target 3 / max 6 at Cancun).</item>
    ///   <item>EIP-7702 — Set EOA account code via type-4 transaction.
    ///   EOAs can delegate to a contract via the <c>0xef0100‖addr</c>
    ///   delegation designator; subsequent CALLs to the EOA execute
    ///   the delegate's code while authorising the EOA as caller.</item>
    ///   <item>EIP-7840 — Blob schedule configuration (genesis-time
    ///   parameter for blob target/max per fork).</item>
    /// </list>
    ///
    /// <para><b>EIPs explicitly NOT activated at Prague:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-7594 (PeerDAS) — Osaka.</item>
    ///   <item>EIP-7825 (Tx gas limit cap) — Osaka.</item>
    /// </list>
    ///
    /// <para><b>Spec sources:</b>
    /// <list type="bullet">
    ///   <item>execution-specs <c>network-upgrades/mainnet-upgrades/prague.md</c>.</item>
    ///   <item>geth <c>params/config.go PragueTime</c>,
    ///   <c>core/vm/jump_table.go newPragueInstructionSet</c>.</item>
    ///   <item>EELS <c>src/ethereum/prague/__init__.py</c>.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class PragueSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            Name = HardforkName.Prague,

            // Opcode + intrinsic gas tables — Prague adds the EIP-7623
            // calldata floor-data-gas computation on top of Cancun, and
            // the opcode table threads EIP-7702 delegation resolution
            // through EXTCODE* / CALL* lookups. BLS12-381 precompiles
            // (EIP-2537) at 0x0b-0x11 are registered through the
            // precompile provider, not as opcodes.
            IntrinsicGas = IntrinsicGasRuleSets.Prague,
            Opcodes = OpcodeHandlerSets.Prague,

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
                new PrecompileSpec { Address = 0x0B, Kind = PrecompileKind.Bls12381_G1Add },
                new PrecompileSpec { Address = 0x0C, Kind = PrecompileKind.Bls12381_G1MultiExp },
                new PrecompileSpec { Address = 0x0D, Kind = PrecompileKind.Bls12381_G2Add },
                new PrecompileSpec { Address = 0x0E, Kind = PrecompileKind.Bls12381_G2MultiExp },
                new PrecompileSpec { Address = 0x0F, Kind = PrecompileKind.Bls12381_Pairing },
                new PrecompileSpec { Address = 0x10, Kind = PrecompileKind.Bls12381_MapFpToG1 },
                new PrecompileSpec { Address = 0x11, Kind = PrecompileKind.Bls12381_MapFp2ToG2 },
            },

            // EIP-4844 type-3 blob validation + EIP-7702 type-4
            // authorisation-list validation (nonzero auth list, valid
            // signatures, magic byte 0x05, chainId 0 or current chain).
            Validation = TransactionValidationRuleSets.Prague,

            // EIP-161 finalisation (Spurious Dragon onwards) — touched
            // empty accounts are deleted at end-of-tx.
            TouchedEmptyCleanup = Eip161TouchedEmptyCleanupRule.Instance,

            // EIP-2200 net-gas SSTORE refunds (Istanbul) — clears refund
            // value comes from SstoreClearsSchedule (4800 at London+).
            SstoreRefund = Eip1283SstoreRefundRule.Instance,

            // EIP-6780 (Cancun) carries forward: SELFDESTRUCT only
            // deletes when invoked in the same transaction as the
            // creating CREATE; otherwise behaves as a balance transfer.
            SelfDestruct = Eip6780SelfDestructRule.Instance,

            // EIP-150 63/64ths gas-forwarding rule (Tangerine Whistle).
            GasForwarding = Eip150GasForwarding.Instance,

            // EIP-2 code-deposit OOG fails the CREATE (Homestead onwards).
            CodeDeposit = HomesteadCodeDepositRule.Instance,
            ContractCreationMaterialise = MaterialiseEmptyOnSuccessRule.Instance,
            BlockHash = Eip2935BlockHashRule.Instance,

            // EIP-7702: resolves 0xef0100‖addr delegation designators
            // during sub-call frame setup and charges the delegate-
            // access gas (cold/warm per EIP-2929).
            // EIP-2935: BlockHash from state storage contract — wired
            // through the transaction setup rules, not call-frame init.
            // EIP-4788 beacon-block-root precompile + access-list
            // pre-warm continue from Cancun.
            CallFrameInit = CallFrameInitRuleSets.Prague,
            TransactionSetup = TransactionSetupRuleSets.Prague,

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

            // EIP-7691 — blob throughput increase: target 6 / max 9
            // blobs per block (up from target 3 / max 6 at Cancun).
            MaxBlobsPerBlock = 9,

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
            // (reserves the prefix for future EOF format). At Prague the
            // 0xef0100 prefix is still rejected as deployed bytecode;
            // EIP-7702 writes the delegation designator via the auth-list
            // setup path, not via CREATE/CREATE2.
            CodePrefix = CodePrefixPolicy.Eip3541RejectEf,
            ReceiptCodec = Eip2718ReceiptCodec.Instance,            // typed receipts incl. 0x04 (SetCode EIP-7702)
            HeaderCodec = PragueBlockHeaderCodec.Instance,          // 21 fields (+requestsHash EIP-7685)
            TransactionDecoder = Eip7702TransactionDecoder.Instance,       // adds 0x04 set-code
            ReceiptConstruction = StatusReceiptConstructionRule.Instance,  // EIP-658: 1-byte status
        };
    }
}
