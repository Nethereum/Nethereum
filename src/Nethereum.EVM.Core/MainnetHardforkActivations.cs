namespace Nethereum.EVM
{
    /// <summary>
    /// Ethereum mainnet fork activation table — chain id 1. Pre-Shanghai forks
    /// activate at a block number; Shanghai onward activate at a timestamp
    /// (post-Paris, blocks are produced on a 12-second slot cadence so
    /// timestamps are the authoritative trigger).
    ///
    /// Forks with no EVM opcode/gas change (FrontierThawing, DaoFork, MuirGlacier,
    /// ArrowGlacier, GrayGlacier) are still reported — the registry aliases them
    /// to the closest EVM-behaviour-equivalent config.
    ///
    /// Returned <see cref="HardforkName"/> is the *highest* fork active at the
    /// given block/timestamp — feed it directly into a
    /// <see cref="HardforkRegistry"/> to obtain the matching config.
    /// </summary>
    public class MainnetChainActivations : IChainActivations
    {
        public static readonly MainnetChainActivations Instance = new MainnetChainActivations();

        // Block-number activations (pre-Shanghai)
        public const long FrontierThawingBlock    = 200_000;
        public const long HomesteadBlock          = 1_150_000;
        public const long DaoForkBlock            = 1_920_000;
        public const long TangerineWhistleBlock   = 2_463_000;
        public const long SpuriousDragonBlock     = 2_675_000;
        public const long ByzantiumBlock          = 4_370_000;
        public const long ConstantinopleBlock     = 7_280_000;
        public const long PetersburgBlock         = 7_280_000;   // Constantinople was reverted at the same block, effectively → Petersburg
        public const long IstanbulBlock           = 9_069_000;
        public const long MuirGlacierBlock        = 9_200_000;
        public const long BerlinBlock             = 12_244_000;
        public const long LondonBlock             = 12_965_000;
        public const long ArrowGlacierBlock       = 13_773_000;
        public const long GrayGlacierBlock        = 15_050_000;
        public const long ParisBlock              = 15_537_394;  // aka "The Merge"

        // Timestamp activations (Shanghai+)
        public const ulong ShanghaiTimestamp = 1_681_338_455;
        public const ulong CancunTimestamp   = 1_710_338_135;
        public const ulong PragueTimestamp   = 1_746_612_311;
        // Osaka (Fusaka network upgrade) mainnet activation:
        // block 23,935,694, timestamp 1,764,798,563 (2025-12-03 21:49:23 UTC).
        // Verified against the Erigon canonical block on this node and the
        // public ethereum.org/ethereum-forks/ activation table. "Fusaka" is
        // the network-upgrade name (Fulu CL + Osaka EL); our HardforkName
        // for the execution-layer fork is Osaka.
        public static readonly ulong? OsakaTimestamp = 1_764_798_563UL;

        // First Osaka BPO (Blob Parameter Only) fork per EIP-7892. Raises
        // BLOB_BASE_FEE_UPDATE_FRACTION from 8,346,193 to 11,684,671.
        // Activation: block 24,179,383, timestamp 1,767,747,671
        // (2026-01-07 01:01:11 UTC). Located by binary search on the
        // Erigon canonical node — first block where the Osaka update
        // fraction stopped matching canonical baseFeePerBlobGas.
        public static readonly ulong? OsakaBpo1Timestamp = 1_767_747_671UL;

        public HardforkName ResolveAt(long blockNumber, ulong timestamp)
        {
            if (OsakaBpo1Timestamp is ulong bpo1Ts && timestamp >= bpo1Ts) return HardforkName.OsakaBpo1;
            if (OsakaTimestamp is ulong osakaTs && timestamp >= osakaTs) return HardforkName.Osaka;
            if (timestamp >= PragueTimestamp)   return HardforkName.Prague;
            if (timestamp >= CancunTimestamp)   return HardforkName.Cancun;
            if (timestamp >= ShanghaiTimestamp) return HardforkName.Shanghai;

            if (blockNumber >= ParisBlock)              return HardforkName.Paris;
            if (blockNumber >= GrayGlacierBlock)        return HardforkName.GrayGlacier;
            if (blockNumber >= ArrowGlacierBlock)       return HardforkName.ArrowGlacier;
            if (blockNumber >= LondonBlock)             return HardforkName.London;
            if (blockNumber >= BerlinBlock)             return HardforkName.Berlin;
            if (blockNumber >= MuirGlacierBlock)        return HardforkName.MuirGlacier;
            if (blockNumber >= IstanbulBlock)           return HardforkName.Istanbul;
            if (blockNumber >= PetersburgBlock)         return HardforkName.Petersburg;
            if (blockNumber >= ByzantiumBlock)          return HardforkName.Byzantium;
            if (blockNumber >= SpuriousDragonBlock)     return HardforkName.SpuriousDragon;
            if (blockNumber >= TangerineWhistleBlock)   return HardforkName.TangerineWhistle;
            if (blockNumber >= DaoForkBlock)            return HardforkName.DaoFork;
            if (blockNumber >= HomesteadBlock)          return HardforkName.Homestead;
            if (blockNumber >= FrontierThawingBlock)    return HardforkName.FrontierThawing;
            return HardforkName.Frontier;
        }
    }

    /// <summary>
    /// Static façade over <see cref="MainnetChainActivations.Instance"/> for callers
    /// that already know they're on mainnet. Prefer <see cref="ChainActivationsRegistry"/>
    /// when chain id is only known at runtime.
    /// </summary>
    public static class MainnetHardforkActivations
    {
        public static HardforkName ResolveAt(long blockNumber, ulong timestamp)
            => MainnetChainActivations.Instance.ResolveAt(blockNumber, timestamp);
    }
}
