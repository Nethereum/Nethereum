using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.Uniswap.UniversalRouter.V4Actions
{
    public class PathKey
    {
        [Parameter("address", "intermediateCurrency", 1)]
        public string IntermediateCurrency { get; set; }

        [Parameter("uint256", "fee", 2)]
        public BigInteger Fee { get; set; }

        [Parameter("int24", "tickSpacing", 3)]
        public int TickSpacing { get; set; }

        [Parameter("address", "hooks", 4)]
        public string Hooks { get; set; }

        [Parameter("bytes", "hookData", 5)]
        public byte[] HookData { get; set; }
    }

}

