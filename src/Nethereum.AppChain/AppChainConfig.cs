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

        public static AppChainConfig Default => new AppChainConfig
        {
            ChainId = 420420,
            BlockGasLimit = 30_000_000,
            BaseFee = 0,
            InitialBalance = BigInteger.Parse("10000000000000000000000")
        };

        public static AppChainConfig CreateWithName(string name, BigInteger chainId)
        {
            return new AppChainConfig
            {
                AppChainName = name,
                ChainId = chainId,
                BlockGasLimit = 30_000_000,
                BaseFee = 0,
                InitialBalance = BigInteger.Parse("10000000000000000000000")
            };
        }
    }
}
