using System.Numerics;
using Nethereum.ChainNode.Hosting.Configuration;
using Nethereum.CoreChain;

namespace Nethereum.DevChain.Configuration
{
    public class DevChainServerConfig
    {
        public const string DefaultMnemonic = "test test test test test test test test test test test junk";
        public const string DefaultServerHardfork = "amsterdam";

        public static readonly long LargestDeploymentAnyForkPermits =
            Nethereum.EVM.Gas.GasConstants.EIP8037_LARGEST_CODE_DEPOSIT_ANY_FORK_PERMITS;

        public DevChainConfig Chain { get; set; } = new DevChainConfig
        {
            ChainId = 31337,
            Hardfork = DefaultServerHardfork,
            MaxTransactionsPerBlock = 10000
        };

        public ChainNodeConfig Node { get; set; } = DevChainDefaultNode();

        public int Port
        {
            get => Node.Rpc.Port;
            set => Node.Rpc.Port = value;
        }

        public string Host
        {
            get => Node.Rpc.Host;
            set => Node.Rpc.Host = value;
        }

        public string Mnemonic { get; set; } = DefaultMnemonic;
        public int AccountCount { get; set; } = 10;
        public bool Verbose { get; set; } = false;
        public string Storage { get; set; } = "sqlite";
        public string DataDir { get; set; } = "./chaindata";
        public bool Persist { get; set; } = false;
        public ForkConfig? Fork { get; set; }

        public bool EngineApiEnabled { get; set; } = false;
        public string? EngineJwtSecretPath { get; set; }
        public int EnginePort { get; set; } = Nethereum.DevChain.Hosting.EngineApiServerConfig.DefaultPort;
        public string? EngineBindAddress { get; set; }

        public int ChainId
        {
            get => (int)Chain.ChainId;
            set => Chain.ChainId = value;
        }

        public string Hardfork
        {
            get => Chain.Hardfork;
            set => Chain.Hardfork = value;
        }

        public long BlockGasLimit
        {
            get => (long)Chain.BlockGasLimit;
            set => Chain.BlockGasLimit = value;
        }

        public bool AutoMine
        {
            get => Chain.AutoMine;
            set => Chain.AutoMine = value;
        }

        public long BlockTime
        {
            get => Chain.BlockTime;
            set => Chain.BlockTime = value;
        }

        public int AutoMineBatchSize
        {
            get => Chain.AutoMineBatchSize;
            set => Chain.AutoMineBatchSize = value;
        }

        public int AutoMineBatchTimeoutMs
        {
            get => Chain.AutoMineBatchTimeoutMs;
            set => Chain.AutoMineBatchTimeoutMs = value;
        }

        public int MaxTransactionsPerBlock
        {
            get => Chain.MaxTransactionsPerBlock;
            set => Chain.MaxTransactionsPerBlock = value;
        }

        public string AccountBalance
        {
            get => Chain.InitialBalance.ToString();
            set => Chain.InitialBalance = BigInteger.Parse(value);
        }

        public BigInteger GetAccountBalance() => Chain.InitialBalance;

        public void SetAccountBalanceEth(string ethAmount)
        {
            var eth = BigInteger.Parse(ethAmount);
            Chain.InitialBalance = eth * BigInteger.Parse("1000000000000000000");
        }

        public DevChainConfig GetLiveChainConfig()
        {
            Chain.ForkUrl = Fork?.Url;
            Chain.ForkBlockNumber = Fork?.BlockNumber;
            Node.Rpc.ApplyTo(Chain);

            return Chain;
        }

        private static ChainNodeConfig DevChainDefaultNode() =>
            new ChainNodeConfig
            {
                Network = new ChainNodeNetworkConfig
                {
                    Serve = false,
                    Discovery = new ChainNodeDiscoveryConfig { DisableDiscv4 = true, DisableDiscv5 = true },
                },
                Sync = new ChainNodeSyncConfig { Mode = SyncMode.None },
            };
    }

    public class ForkConfig
    {
        public string? Url { get; set; }
        public long? BlockNumber { get; set; }
        public bool AutoDetectArchive { get; set; } = true;
    }
}
