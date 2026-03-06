using System.Numerics;
using Nethereum.DevChain;

namespace Nethereum.DevChain.Server.Configuration
{
    public class DevChainServerConfig
    {
        public const string DefaultMnemonic = "test test test test test test test test test test test junk";

        public int Port { get; set; } = 8545;
        public string Host { get; set; } = "127.0.0.1";
        public int ChainId { get; set; } = 31337;
        public long BlockGasLimit { get; set; } = 30_000_000;
        public bool AutoMine { get; set; } = true;
        public long BlockTime { get; set; } = 0;
        public int AccountCount { get; set; } = 10;
        public string Mnemonic { get; set; } = DefaultMnemonic;
        public string AccountBalance { get; set; } = "10000000000000000000000";
        public ForkConfig? Fork { get; set; }
        public bool Verbose { get; set; } = false;
        public int AutoMineBatchSize { get; set; } = 1;
        public int AutoMineBatchTimeoutMs { get; set; } = 10;
        public int MaxTransactionsPerBlock { get; set; } = 10000;
        public string Storage { get; set; } = "sqlite";
        public string DataDir { get; set; } = "./chaindata";
        public bool Persist { get; set; } = false;

        public BigInteger GetAccountBalance()
        {
            return BigInteger.Parse(AccountBalance);
        }

        public void SetAccountBalanceEth(string ethAmount)
        {
            var eth = BigInteger.Parse(ethAmount);
            AccountBalance = (eth * BigInteger.Parse("1000000000000000000")).ToString();
        }

        public DevChainConfig ToDevChainConfig()
        {
            return new DevChainConfig
            {
                ChainId = ChainId,
                BlockGasLimit = BlockGasLimit,
                AutoMine = AutoMine,
                BlockTime = BlockTime,
                AutoMineBatchSize = AutoMineBatchSize,
                AutoMineBatchTimeoutMs = AutoMineBatchTimeoutMs,
                MaxTransactionsPerBlock = MaxTransactionsPerBlock,
                InitialBalance = GetAccountBalance(),
                ForkUrl = Fork?.Url,
                ForkBlockNumber = Fork?.BlockNumber
            };
        }
    }

    public class ForkConfig
    {
        public string? Url { get; set; }
        public long? BlockNumber { get; set; }
        public bool AutoDetectArchive { get; set; } = true;
    }
}
