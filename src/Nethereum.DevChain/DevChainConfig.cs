using Nethereum.CoreChain;

namespace Nethereum.DevChain
{
    public class DevChainConfig : ChainConfig
    {
        public bool AutoMine { get; set; } = true;
        public int MaxTransactionsPerBlock { get; set; } = 100;
        public long BlockTime { get; set; } = 0;

        public long TimeOffset { get; set; } = 0;
        public long? NextBlockTimestamp { get; set; }

        public string ForkUrl { get; set; }
        public long? ForkBlockNumber { get; set; }
        public bool IsForkEnabled => !string.IsNullOrEmpty(ForkUrl);

        public static DevChainConfig Default => new DevChainConfig();

        public static DevChainConfig Hardhat => new DevChainConfig
        {
            ChainId = 31337,
            BlockGasLimit = 30_000_000,
            BaseFee = 1_000_000_000,
            AutoMine = true
        };

        public static DevChainConfig Anvil => new DevChainConfig
        {
            ChainId = 31337,
            BlockGasLimit = 30_000_000,
            BaseFee = 1_000_000_000,
            AutoMine = true
        };
    }
}
