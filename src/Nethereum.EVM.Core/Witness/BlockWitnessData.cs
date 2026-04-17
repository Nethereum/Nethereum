using System.Collections.Generic;

namespace Nethereum.EVM.Witness
{
    /// <summary>
    /// Unified witness format for single-tx and block proofs.
    /// Transactions are signed RLP-encoded bytes — decoded at execution,
    /// original bytes used for the transactions trie.
    /// </summary>
    public class BlockWitnessData
    {
        // Block context
        public long BlockNumber { get; set; }
        public long Timestamp { get; set; }
        public long BaseFee { get; set; }
        public long BlockGasLimit { get; set; }
        public long ChainId { get; set; }
        public string Coinbase { get; set; }
        public byte[] Difficulty { get; set; }
        public byte[] PreStateRoot { get; set; }
        public byte[] ParentHash { get; set; }
        public byte[] ExtraData { get; set; }
        public byte[] MixHash { get; set; }
        public byte[] Nonce { get; set; }

        // Fork-specific block data (optional — null when feature is inactive)
        public List<BlockWithdrawal> Withdrawals { get; set; }
        public long? BlobGasUsed { get; set; }
        public long? ExcessBlobGas { get; set; }
        public byte[] ParentBeaconBlockRoot { get; set; }
        public byte[] RequestsHash { get; set; }

        // Transactions — 1 for single-tx, N for block
        public List<BlockWitnessTransaction> Transactions { get; set; } = new List<BlockWitnessTransaction>();

        // Pre-state accounts (with optional Merkle proofs). BLOCKHASH data lives
        // in the EIP-2935 history contract's storage — no separate block-hash map.
        public List<WitnessAccount> Accounts { get; set; } = new List<WitnessAccount>();

        // --- Feature configuration (optional — null = infer from block data) ---
        public BlockFeatureConfig Features { get; set; }

        // --- Execution flags ---

        /// <summary>
        /// Verify witness Merkle proofs against PreStateRoot before execution.
        /// Includes account and storage inclusion/exclusion proofs, and enforces
        /// that execution only accesses state covered by the witness.
        /// Catches forged, incomplete, or tampered witnesses.
        /// </summary>
        public bool VerifyWitnessProofs { get; set; }

        /// <summary>
        /// After execution, compute the post-state root by updating the Patricia
        /// trie with modified account and storage values.
        /// Required for any state transition proof or root validation.
        /// </summary>
        public bool ComputePostStateRoot { get; set; }

        /// <summary>
        /// Produce full block commitment outputs: post-state root, transactions
        /// root, receipts root, and block hash.
        /// Implies ComputePostStateRoot = true.
        /// In trustless mode, also requires VerifyWitnessProofs = true.
        /// </summary>
        public bool ProduceBlockCommitments { get; set; }
    }

    /// <summary>
    /// A transaction in the block witness.
    /// From = sender address (pre-recovered by the witness generator).
    /// RlpEncoded = signed RLP bytes exactly as on-chain — source of truth.
    /// </summary>
    public class BlockFeatureConfig
    {
        public HardforkName Fork { get; set; } = HardforkName.Unspecified;
        public int MaxBlobsPerBlock { get; set; } = 6;
        public WitnessStateTreeType StateTree { get; set; } = WitnessStateTreeType.Patricia;
        public WitnessHashFunction HashFunction { get; set; } = WitnessHashFunction.Keccak;

        public static BlockFeatureConfig Cancun => new BlockFeatureConfig
        {
            Fork = HardforkName.Cancun, MaxBlobsPerBlock = 6
        };

        public static BlockFeatureConfig Prague => new BlockFeatureConfig
        {
            Fork = HardforkName.Prague, MaxBlobsPerBlock = 9
        };

        public static BlockFeatureConfig Osaka => new BlockFeatureConfig
        {
            Fork = HardforkName.Osaka, MaxBlobsPerBlock = 9
        };

        public static BlockFeatureConfig BinaryBlake3(HardforkName fork = HardforkName.Osaka) => new BlockFeatureConfig
        {
            Fork = fork, MaxBlobsPerBlock = 9,
            StateTree = WitnessStateTreeType.Binary, HashFunction = WitnessHashFunction.Blake3
        };

        public static BlockFeatureConfig BinaryPoseidon(HardforkName fork = HardforkName.Osaka) => new BlockFeatureConfig
        {
            Fork = fork, MaxBlobsPerBlock = 9,
            StateTree = WitnessStateTreeType.Binary, HashFunction = WitnessHashFunction.Poseidon
        };
    }

    public class BlockWithdrawal
    {
        public ulong Index { get; set; }
        public ulong ValidatorIndex { get; set; }
        public string Address { get; set; }
        public ulong AmountInGwei { get; set; }
    }

    public class BlockWitnessTransaction
    {
        public string From { get; set; }
        public byte[] RlpEncoded { get; set; }

        /// <summary>
        /// Pre-recovered authority addresses for an EIP-7702 type-4 transaction,
        /// parallel to <c>Transaction7702.AuthorisationList</c>. The witness generator
        /// runs full signature validation (y_parity, canonical r/s bounds) and
        /// ECDSA public-key recovery on the host side, storing either the recovered
        /// authority address or <c>null</c> for an invalid tuple.
        ///
        /// The zkVM sync executor reads these addresses instead of calling
        /// <c>Nethereum.Signer</c>, which is not available in the minimal build.
        /// State-dependent validation (chain id, nonce, existing code) still runs
        /// in-guest.
        ///
        /// For non-7702 transactions this list is null.
        /// </summary>
        public List<string> AuthorisationAuthorities { get; set; }
    }
}
