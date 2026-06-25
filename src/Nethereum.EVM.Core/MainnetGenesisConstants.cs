namespace Nethereum.EVM
{
    /// <summary>
    /// Canonical, never-changing constants of the Ethereum mainnet (chainId 1)
    /// genesis block — pinned against the canonical mainnet genesis. These
    /// values are part of the
    /// permanent protocol record and serve as the canary that any state-root
    /// machinery must reproduce when fed the mainnet alloc.
    /// <para>
    /// The 8,893-entry allocation itself is too large to embed as source; it
    /// ships as a JSON data file alongside test fixtures. The headline
    /// constants below are everything production code typically needs.
    /// </para>
    /// </summary>
    public static class MainnetGenesisConstants
    {
        /// <summary>Chain id of the public Ethereum mainnet.</summary>
        public const long ChainId = 1;

        /// <summary>
        /// Patricia state root of the genesis allocation. Computing this from the
        /// 8,893 alloc entries and matching it byte-for-byte is the
        /// state-machinery canary.
        /// </summary>
        public const string StateRootHex =
            "0xd7f8974fb5ac78d9ac099b9ad5018bedc2ce0a72dad1827a1709da30580f0544";

        /// <summary>Keccak-256 hash of the canonical genesis block header.</summary>
        public const string BlockHashHex =
            "0xd4e56740f876aef8c010b86a40d5f56745a118d0906a34e69aec8c0db1cb8fa3";

        /// <summary>Initial mining difficulty = 2^34 = 17,179,869,184.</summary>
        public const ulong Difficulty = 17_179_869_184UL;

        /// <summary>Genesis gas limit (0x1388).</summary>
        public const long GasLimit = 5000;

        /// <summary>Genesis block timestamp — zero, the unix epoch.</summary>
        public const ulong Timestamp = 0;

        /// <summary>Genesis nonce = the answer to life, the universe, and everything (0x42).</summary>
        public const string NonceHex = "0x0000000000000042";

        /// <summary>Genesis mixHash — all zeros, no PoW seal on block 0.</summary>
        public const string MixHashHex =
            "0x0000000000000000000000000000000000000000000000000000000000000000";

        /// <summary>Genesis coinbase — the zero address; no miner reward at block 0.</summary>
        public const string CoinbaseHex = "0x0000000000000000000000000000000000000000";

        /// <summary>
        /// Genesis extra-data — the 32-byte SHA3 of an unknown pre-launch artefact,
        /// Often hex-quoted but rarely interpreted.
        /// </summary>
        public const string ExtraDataHex =
            "0x11bbe8db4e347b4e8c937c1c8370e4b5ed33adb3db69cbdb7a38e1e50b1b82fa";

        /// <summary>Number of accounts in the genesis allocation.</summary>
        public const int AllocAccountCount = 8893;

        /// <summary>Sum of every genesis account's balance in wei — ~72.01 million ETH.</summary>
        public const string TotalAllocBalanceWei = "72009990499480000000000000";
    }
}
