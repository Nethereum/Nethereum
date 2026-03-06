using System.Numerics;
using Nethereum.EVM;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public class ChainConfig
    {
        public BigInteger ChainId { get; set; } = 1337;
        public string Coinbase { get; set; } = AddressUtil.ZERO_ADDRESS;
        public BigInteger BlockGasLimit { get; set; } = 30_000_000;
        public BigInteger BaseFee { get; set; } = 1_000_000_000; // 1 Gwei
        public BigInteger SuggestedPriorityFee { get; set; } = 1_000_000_000; // 1 Gwei
        public BigInteger InitialBalance { get; set; } = BigInteger.Parse("10000000000000000000000"); // 10000 ETH
        public string Hardfork { get; set; } = "prague";

        public HardforkConfig GetHardforkConfig() => HardforkConfig.FromName(Hardfork);
    }
}
