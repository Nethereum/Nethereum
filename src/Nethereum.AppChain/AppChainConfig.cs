using System.Numerics;
using Nethereum.CoreChain;

namespace Nethereum.AppChain
{
    public class AppChainConfig : ChainConfig
    {
        public string AppChainName { get; set; } = "AppChain";

        public string WorldAddress { get; set; } = "0x4200000000000000000000000000000000000001";

        public string? SequencerAddress { get; set; }

        public byte[]? GenesisHash { get; set; }

        public override BigInteger BlockGasLimit
        {
            get => _blockGasLimit ?? BlockGasLimitLargeEnoughToDeployAt(NewestForkThisChainRuns);
            set => _blockGasLimit = value;
        }

        public static AppChainConfig Default => new AppChainConfig
        {
            ChainId = 420420,
            BaseFee = 0,
            InitialBalance = BigInteger.Parse("10000000000000000000000")
        };

        public static AppChainConfig CreateWithName(string name, BigInteger chainId)
        {
            return new AppChainConfig
            {
                AppChainName = name,
                ChainId = chainId,
                BaseFee = 0,
                InitialBalance = BigInteger.Parse("10000000000000000000000")
            };
        }
    }
}
