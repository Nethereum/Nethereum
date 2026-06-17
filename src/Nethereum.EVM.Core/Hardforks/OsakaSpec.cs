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
    /// Declarative <see cref="HardforkSpec"/> for the Osaka hardfork
    /// ("Fusaka" — Fulu + Osaka — mainnet activation 2025/2026).
    ///
    /// <para><b>EIPs activated at Osaka (execution-layer subset):</b></para>
    /// <list type="bullet">
    ///   <item>EIP-7594 — PeerDAS (Peer Data Availability Sampling).
    ///   Sampling-based DA for blob data; the execution-layer change is
    ///   the blob-schedule + propagation contract — no opcode change.</item>
    ///   <item>EIP-7825 — Transaction gas limit cap. A single transaction
    ///   is rejected if its declared <c>gasLimit</c> exceeds <b>16,777,216</b>
    ///   gas (2^24). Enforced at tx validation, before EVM execution.</item>
    ///   <item>EIP-7883 — ModExp gas repricing. Increases the floor cost
    ///   and adjusts the multiplication-complexity formula for the
    ///   <c>0x05</c> ModExp precompile (handled in
    ///   <see cref="OpcodeHandlerSets.Osaka"/> via the
    ///   <c>Eip7883ModExpGasCalculator</c>).</item>
    ///   <item>EIP-7918 — Blob base-fee floor. Imposes a minimum blob
    ///   base fee so the fee market cannot collapse to zero under low
    ///   demand; price-feedback only, no EVM opcode change.</item>
    ///   <item>EIP-7951 — P256VERIFY precompile at <c>0x100</c>. Verifies
    ///   ECDSA secp256r1 signatures; gas calculator wired in
    ///   <c>PrecompileGasCalculatorSets.Osaka</c>. Enables WebAuthn and
    ///   passkey-style authentication directly from smart contracts.</item>
    /// </list>
    ///
    /// <para><b>Carried over from Prague:</b></para>
    /// <list type="bullet">
    ///   <item>EIP-2537 BLS12-381 precompiles.</item>
    ///   <item>EIP-2935 historical BLOCKHASH storage contract.</item>
    ///   <item>EIP-6110 / EIP-7002 / EIP-7251 / EIP-7685 execution-layer
    ///   requests bundle.</item>
    ///   <item>EIP-7623 calldata floor cost.</item>
    ///   <item>EIP-7691 blob throughput (target 6 / max 9).</item>
    ///   <item>EIP-7702 EOA delegation via type-4 tx.</item>
    /// </list>
    ///
    /// <para><b>Spec sources:</b>
    /// <list type="bullet">
    ///   <item>execution-specs <c>network-upgrades/mainnet-upgrades/osaka.md</c>.</item>
    ///   <item>geth <c>params/config.go OsakaTime</c>,
    ///   <c>core/vm/jump_table.go newOsakaInstructionSet</c>.</item>
    ///   <item>EELS <c>src/ethereum/osaka/__init__.py</c>.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class OsakaSpec
    {
        public static readonly HardforkSpec Instance = new HardforkSpec
        {
            Name = HardforkName.Osaka,

            // Opcode + intrinsic gas tables — Osaka swaps the ModExp
            // (0x05) precompile gas calculator to the EIP-7883 schedule;
            // the rest of the table is identical to Prague (EIP-7623
            // calldata floor cost + EIP-7702 delegation resolution
            // through EXTCODE* / CALL* lookups).
            IntrinsicGas = IntrinsicGasRuleSets.Osaka,
            Opcodes = OpcodeHandlerSets.Osaka,

            Precompiles = new[]
            {
                new PrecompileSpec { Address = 0x01, Kind = PrecompileKind.Ecrecover },
                new PrecompileSpec { Address = 0x02, Kind = PrecompileKind.Sha256 },
                new PrecompileSpec { Address = 0x03, Kind = PrecompileKind.Ripemd160 },
                new PrecompileSpec { Address = 0x04, Kind = PrecompileKind.Identity },
                new PrecompileSpec { Address = 0x05, Kind = PrecompileKind.ModExp_Eip7883 },
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
                new PrecompileSpec { Address = 0x100, Kind = PrecompileKind.P256Verify },
            },

            // Prague rule sets (EIP-4844 type-3 blob validation +
            // EIP-7702 type-4 authorisation-list validation) plus
            // EIP-7825: reject any tx whose gasLimit exceeds 2^24
            // (16,777,216) gas — enforced before EVM dispatch.
            Validation = TransactionValidationRuleSets.Osaka,

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

            // EIP-7702 delegation resolution + EIP-2935 BLOCKHASH storage
            // contract setup + EIP-4788 beacon-block-root precompile —
            // all inherited from Prague. Osaka does not add new setup or
            // call-frame initialisation steps; EIP-7594 PeerDAS is a
            // network-layer concern handled outside the EVM.
            CallFrameInit = CallFrameInitRuleSets.Osaka,
            TransactionSetup = TransactionSetupRuleSets.Osaka,

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

            // EIP-7691 — blob throughput retained from Prague:
            // target 6 / max 9 blobs per block. EIP-7594 PeerDAS makes
            // the block-level cap a soft DA-sampling parameter but the
            // execution-layer enforcement value is unchanged.
            MaxBlobsPerBlock = 9,

            // EIP-161 (Spurious Dragon) — freshly-created contracts start
            // at nonce 1 so they are never "empty" by EIP-158 definition.
            ContractInitialNonce = 1,

            // EIP-2200 (Istanbul) 2300-gas SSTORE reentrancy sentry.
            EnforceSstoreSentry = SstoreSentryPolicy.Eip2200Active,

            // EIP-3651 (Shanghai) — coinbase pre-warmed in access list.
            CoinbaseAccess = CoinbaseAccessPolicy.Eip3651Warm,

            // EIP-1559 (London) — base fee burnt, priority fee to miner.
            // EIP-7918 imposes a minimum blob base fee but does not
            // change the EIP-1559 burn semantics for non-blob fees.
            BaseFee = BaseFeePolicy.Eip1559Burnt,

            // EIP-161 (Spurious Dragon) — empty accounts deleted on touch.
            EmptyAccount = EmptyAccountPolicy.Eip161Clear,

            // EIP-3541 (London) — reject deployed code starting with 0xEF
            // (reserves the prefix for future EOF format). At Osaka the
            // 0xef0100 delegation prefix is still rejected as deployed
            // bytecode; EIP-7702 writes the delegation designator via the
            // auth-list setup path, not via CREATE/CREATE2.
            CodePrefix = CodePrefixPolicy.Eip3541RejectEf,
        };
    }
}
