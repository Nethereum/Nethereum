using Nethereum.EVM;

namespace Nethereum.DevP2P.IntegrationTests
{
    /// <summary>
    /// Compatibility shim: the production HardforkResolver type was replaced by
    /// the IChainActivations interface in an earlier refactor (task #47). Five
    /// test files in this assembly still reference HardforkResolver.HiveTestdata()
    /// and have not been migrated. This shim restores compilation by exposing
    /// the same factory shape but returning an IChainActivations. The proper
    /// fix is to inline the activations into each test (see HiveTestdataFixture
    /// in CoreChain.RocksDB.UnitTests for the reference shape).
    /// </summary>
    internal static class HardforkResolver
    {
        public static IChainActivations HiveTestdata() => new HiveTestdataActivations();
    }

    internal sealed class HiveTestdataActivations : IChainActivations
    {
        public const long HomesteadBlock = 0;
        public const long TangerineWhistleBlock = 6;
        public const long SpuriousDragonBlock = 12;
        public const long ByzantiumBlock = 18;
        public const long ConstantinopleBlock = 24;
        public const long PetersburgBlock = 30;
        public const long IstanbulBlock = 36;
        public const long MuirGlacierBlock = 42;
        public const long BerlinBlock = 48;
        public const long LondonBlock = 54;
        public const long ArrowGlacierBlock = 60;
        public const long GrayGlacierBlock = 66;
        public const long ParisBlock = 72;
        public const ulong ShanghaiTimestamp = 780;
        public const ulong CancunTimestamp = 840;

        public HardforkName ResolveAt(long blockNumber, ulong timestamp)
        {
            if (timestamp >= CancunTimestamp) return HardforkName.Cancun;
            if (timestamp >= ShanghaiTimestamp) return HardforkName.Shanghai;
            if (blockNumber >= ParisBlock) return HardforkName.Paris;
            if (blockNumber >= GrayGlacierBlock) return HardforkName.GrayGlacier;
            if (blockNumber >= ArrowGlacierBlock) return HardforkName.ArrowGlacier;
            if (blockNumber >= LondonBlock) return HardforkName.London;
            if (blockNumber >= BerlinBlock) return HardforkName.Berlin;
            if (blockNumber >= MuirGlacierBlock) return HardforkName.MuirGlacier;
            if (blockNumber >= IstanbulBlock) return HardforkName.Istanbul;
            if (blockNumber >= PetersburgBlock) return HardforkName.Petersburg;
            if (blockNumber >= ConstantinopleBlock) return HardforkName.Constantinople;
            if (blockNumber >= ByzantiumBlock) return HardforkName.Byzantium;
            if (blockNumber >= SpuriousDragonBlock) return HardforkName.SpuriousDragon;
            if (blockNumber >= TangerineWhistleBlock) return HardforkName.TangerineWhistle;
            if (blockNumber >= HomesteadBlock) return HardforkName.Homestead;
            return HardforkName.Frontier;
        }
    }
}
