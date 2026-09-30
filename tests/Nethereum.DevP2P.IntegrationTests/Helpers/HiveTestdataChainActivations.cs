using Nethereum.EVM;

namespace Nethereum.DevP2P.IntegrationTests.Helpers
{
    public class HiveTestdataChainActivations : IChainActivations
    {
        public static readonly HiveTestdataChainActivations Instance = new HiveTestdataChainActivations();

        public const ulong ShanghaiTimestamp = 0;
        public const ulong CancunTimestamp   = 60;
        public const ulong PragueTimestamp   = 120;

        public HardforkName ResolveAt(long blockNumber, ulong timestamp)
        {
            if (timestamp >= PragueTimestamp) return HardforkName.Prague;
            if (timestamp >= CancunTimestamp) return HardforkName.Cancun;
            return HardforkName.Shanghai;
        }
    }
}
