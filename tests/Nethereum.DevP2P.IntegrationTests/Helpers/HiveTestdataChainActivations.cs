using Nethereum.EVM;

namespace Nethereum.DevP2P.IntegrationTests.Helpers
{
    /// <summary>
    /// <see cref="IChainActivations"/> for go-ethereum's
    /// <c>cmd/devp2p/internal/ethtest/testdata</c> Hive chain. The schedule
    /// matches the fork blocks and timestamps in the testdata <c>forkenv.json</c>,
    /// so block re-execution through the <c>BlockProcessor</c> runs each block
    /// under the same fork rules Geth used to produce the testdata.
    /// <para>
    /// Block activations are tightly packed (every 6 blocks) so the 500-block
    /// fixture chain crosses every historical hardfork without needing a
    /// production-sized block budget. Shanghai/Cancun ride on tiny timestamps
    /// (780 / 840) so the fixture also exercises the time-activated path.
    /// </para>
    /// </summary>
    public class HiveTestdataChainActivations : IChainActivations
    {
        public static readonly HiveTestdataChainActivations Instance = new HiveTestdataChainActivations();

        // Block-number activations (forkenv.json HIVE_FORK_* keys)
        public const long HomesteadBlock         = 0;
        public const long TangerineWhistleBlock  = 6;
        public const long SpuriousDragonBlock    = 12;
        public const long ByzantiumBlock         = 18;
        public const long ConstantinopleBlock    = 24;
        public const long PetersburgBlock        = 30;
        public const long IstanbulBlock          = 36;
        public const long MuirGlacierBlock       = 42;
        public const long BerlinBlock            = 48;
        public const long LondonBlock            = 54;
        public const long ArrowGlacierBlock      = 60;
        public const long GrayGlacierBlock       = 66;
        public const long ParisBlock             = 72;

        // Timestamp activations (HIVE_SHANGHAI_TIMESTAMP / HIVE_CANCUN_TIMESTAMP)
        public const ulong ShanghaiTimestamp = 780;
        public const ulong CancunTimestamp   = 840;

        public HardforkName ResolveAt(long blockNumber, ulong timestamp)
        {
            if (timestamp >= CancunTimestamp)   return HardforkName.Cancun;
            if (timestamp >= ShanghaiTimestamp) return HardforkName.Shanghai;

            if (blockNumber >= ParisBlock)            return HardforkName.Paris;
            if (blockNumber >= GrayGlacierBlock)      return HardforkName.GrayGlacier;
            if (blockNumber >= ArrowGlacierBlock)     return HardforkName.ArrowGlacier;
            if (blockNumber >= LondonBlock)           return HardforkName.London;
            if (blockNumber >= BerlinBlock)           return HardforkName.Berlin;
            if (blockNumber >= MuirGlacierBlock)      return HardforkName.MuirGlacier;
            if (blockNumber >= IstanbulBlock)         return HardforkName.Istanbul;
            if (blockNumber >= PetersburgBlock)       return HardforkName.Petersburg;
            if (blockNumber >= ConstantinopleBlock)   return HardforkName.Constantinople;
            if (blockNumber >= ByzantiumBlock)        return HardforkName.Byzantium;
            if (blockNumber >= SpuriousDragonBlock)   return HardforkName.SpuriousDragon;
            if (blockNumber >= TangerineWhistleBlock) return HardforkName.TangerineWhistle;
            if (blockNumber >= HomesteadBlock)        return HardforkName.Homestead;
            return HardforkName.Frontier;
        }
    }
}
