using System.Threading;
using Nethereum.CoreChain;

namespace Nethereum.DevChain
{
    public class NextBlockOverrides
    {
        public long? Timestamp { get; set; }
        public System.Numerics.BigInteger? BaseFee { get; set; }
        public byte[] PrevRandao { get; set; }
        public string Coinbase { get; set; }
    }

    public class DevChainConfig : ChainConfig
    {
        private readonly object _nextBlockLock = new();

        public bool AutoMine { get; set; } = true;
        public int MaxTransactionsPerBlock { get; set; } = 100;
        public long BlockTime { get; set; } = 0;

        public int AutoMineBatchSize { get; set; } = 1;
        public int AutoMineBatchTimeoutMs { get; set; } = 10;

        private long _timeOffset;
        public long TimeOffset
        {
            get => Interlocked.Read(ref _timeOffset);
            set => Interlocked.Exchange(ref _timeOffset, value);
        }
        public long? NextBlockTimestamp { get; set; }
        public System.Numerics.BigInteger? NextBlockBaseFee { get; set; }
        public byte[] NextBlockPrevRandao { get; set; }
        public string NextBlockCoinbase { get; set; }

        public string ForkUrl { get; set; }
        public long? ForkBlockNumber { get; set; }
        public bool IsForkEnabled => !string.IsNullOrEmpty(ForkUrl);

        public NextBlockOverrides ConsumeNextBlockOverrides()
        {
            lock (_nextBlockLock)
            {
                var overrides = new NextBlockOverrides
                {
                    Timestamp = NextBlockTimestamp,
                    BaseFee = NextBlockBaseFee,
                    PrevRandao = NextBlockPrevRandao,
                    Coinbase = NextBlockCoinbase
                };
                NextBlockTimestamp = null;
                NextBlockBaseFee = null;
                NextBlockPrevRandao = null;
                NextBlockCoinbase = null;
                return overrides;
            }
        }

        public void SetNextBlockTimestamp(long timestamp)
        {
            lock (_nextBlockLock) { NextBlockTimestamp = timestamp; }
        }

        public void SetNextBlockBaseFee(System.Numerics.BigInteger baseFee)
        {
            lock (_nextBlockLock) { NextBlockBaseFee = baseFee; }
        }

        public void SetNextBlockPrevRandao(byte[] prevRandao)
        {
            lock (_nextBlockLock) { NextBlockPrevRandao = prevRandao; }
        }

        public void SetNextBlockCoinbase(string coinbase)
        {
            lock (_nextBlockLock) { NextBlockCoinbase = coinbase; }
        }

        public long AddTimeOffset(long seconds)
        {
            return Interlocked.Add(ref _timeOffset, seconds);
        }

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
