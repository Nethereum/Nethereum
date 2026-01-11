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
        public int AccountCount { get; set; } = 10;
        public string Mnemonic { get; set; } = DefaultMnemonic;
        public string AccountBalance { get; set; } = "10000000000000000000000";
        public ForkConfig? Fork { get; set; }
        public bool Verbose { get; set; } = false;

        public BigInteger GetAccountBalance()
        {
            return BigInteger.Parse(AccountBalance);
        }

        public DevChainConfig ToDevChainConfig()
        {
            return new DevChainConfig
            {
                ChainId = ChainId,
                BlockGasLimit = BlockGasLimit,
                AutoMine = AutoMine,
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
